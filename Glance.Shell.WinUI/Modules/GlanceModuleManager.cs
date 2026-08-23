using Glance.Application.Abstractions;
using Glance.Transcription;
using Elysium.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceModuleManager :
    IAsyncDisposable
{
    private static readonly TimeSpan copySettleDelay = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherQueue dispatcherQueue;
    private readonly IReadOnlyList<FileSystemWatcher> watchers;
    private readonly GlanceRuntimeServiceProvider runtimeServices;
    private readonly HashSet<string> knownPackages = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly ILogger<GlanceModuleManager> logger;
    private readonly GlanceBridgeRouter bridgeRouter;
    private readonly GlanceAssistantCommandService assistantCommandService;
    private readonly GlanceAssistantService assistantService;
    private readonly GlanceActionService actionService;
    private readonly GlanceIntentService intentService;
    private readonly IGlanceInspectorProviderPreferences inspectorProviderPreferences;
    private readonly GlanceInspectorProviderRegistry inspectorProviderRegistry;
    private readonly IGlanceQuickConverterPreferences quickConverterPreferences;
    private readonly GlanceQuickConverterRegistry quickConverterRegistry;
    private readonly ITranscriptionProviderRegistry transcriptionProviderRegistry;
    private readonly List<LoadedModulePackage> loadedPackages = [];
    private readonly ModulePreferenceService preferences;
    private readonly ModuleInstallationService installations;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> settingsWriter;
    private readonly IServiceProvider applicationServices;
    private readonly Dictionary<string, CancellationTokenSource> pendingPackages = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly object synchronization = new();

    public GlanceModuleManager(IServiceProvider applicationServices, GlanceRuntimeServiceProvider runtimeServices, DispatcherQueue dispatcherQueue, ILogger<GlanceModuleManager> logger)
    {
        this.applicationServices = applicationServices;
        this.runtimeServices = runtimeServices;
        this.dispatcherQueue = dispatcherQueue;
        this.logger = logger;
        preferences = applicationServices.GetRequiredService<ModulePreferenceService>();
        installations = applicationServices.GetRequiredService<ModuleInstallationService>();
        settings = applicationServices.GetRequiredService<GlanceSettings>();
        settingsWriter = applicationServices.GetRequiredService<IWritableOptions<GlanceSettings>>();
        bridgeRouter = applicationServices.GetRequiredService<GlanceBridgeRouter>();
        assistantCommandService = applicationServices.GetRequiredService<GlanceAssistantCommandService>();
        assistantService = applicationServices.GetRequiredService<GlanceAssistantService>();
        actionService = applicationServices.GetRequiredService<GlanceActionService>();
        intentService = applicationServices.GetRequiredService<GlanceIntentService>();
        inspectorProviderPreferences = applicationServices.GetRequiredService<IGlanceInspectorProviderPreferences>();
        inspectorProviderRegistry = applicationServices.GetRequiredService<GlanceInspectorProviderRegistry>();
        quickConverterPreferences = applicationServices.GetRequiredService<IGlanceQuickConverterPreferences>();
        quickConverterRegistry = applicationServices.GetRequiredService<GlanceQuickConverterRegistry>();
        transcriptionProviderRegistry = applicationServices.GetRequiredService<ITranscriptionProviderRegistry>();
        installations.ConfigureInstaller(InstallPackageAsync);

        _ = Directory.CreateDirectory(GlanceModuleLoader.UserModulesDirectory);
        watchers = (FileSystemWatcher[])[.. GlanceModuleLoader.ModuleDirectories
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreateWatcher)];
    }

    public async Task LoadStartupModulesAsync()
    {
        string[] preferredPackageOrder = [.. settings.Modules
            .OrderByDescending(preference => preference.IsEnabled)
            .Select(preference => preference.Id)];
        using IEnumerator<GlanceModuleLoadResult> results = GlanceModuleLoader.Load(preferredPackageOrder).GetEnumerator();

        while (await DispatchAsync(async () =>
        {
            if (!results.MoveNext())
            {
                return false;
            }

            _ = await InstallAsync(results.Current);
            return true;
        }, DispatcherQueuePriority.Low).ConfigureAwait(false))
        {
        }
    }

    public void StartWatching()
    {
        foreach (FileSystemWatcher watcher in watchers)
        {
            watcher.EnableRaisingEvents = true;

            foreach (string packagePath in Directory.EnumerateFiles(watcher.Path, "*.glance", SearchOption.AllDirectories))
            {
                SchedulePackage(packagePath);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (FileSystemWatcher watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= HandlePackageChanged;
            watcher.Changed -= HandlePackageChanged;
            watcher.Renamed -= HandlePackageRenamed;
            watcher.Error -= HandleWatcherError;
            watcher.Dispose();
        }

        CancellationTokenSource[] pending;

        lock (synchronization)
        {
            pending = [.. pendingPackages.Values];
            pendingPackages.Clear();
        }

        foreach (CancellationTokenSource cancellation in pending)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        foreach (LoadedModulePackage package in loadedPackages.AsEnumerable().Reverse())
        {
            DisposeRegistrations(package.TranscriptionRegistrations);
            await DisposeRuntimeAsync(package.Runtime);
        }
    }

    private async Task<ModuleInstallResult> InstallAsync(GlanceModuleLoadResult result)
    {
        GlanceModuleRuntime? runtime = null;
        string? registeredPackageId = null;
        string[] registeredInstallationIds = [];
        IReadOnlyList<IGlanceQuickConverter> quickConverters = [];
        bool quickConvertersRegistered = false;
        IReadOnlyList<IGlanceInspectorProvider> inspectorProviders = [];
        bool inspectorProvidersRegistered = false;
        List<IDisposable> transcriptionRegistrations = [];

        try
        {
            runtime = await GlanceModuleRuntime.CreateAsync(runtimeServices, result.Modules);
            IReadOnlyList<IGlanceComponent> components = (IGlanceComponent[])[.. runtime.Services.GetServices<IGlanceComponent>()];
            IReadOnlyList<IGlanceAssistantProvider> assistantProviders = (IGlanceAssistantProvider[])[.. runtime.Services.GetServices<IGlanceAssistantProvider>()];
            IReadOnlyList<IGlanceAssistantCommandHandler> assistantCommandHandlers = (IGlanceAssistantCommandHandler[])[.. runtime.Services.GetServices<IGlanceAssistantCommandHandler>()];
            IReadOnlyList<IGlanceAssistantSemanticResolver> assistantSemanticResolvers = (IGlanceAssistantSemanticResolver[])[.. runtime.Services.GetServices<IGlanceAssistantSemanticResolver>()];
            quickConverters = (IGlanceQuickConverter[])[.. runtime.Services.GetServices<IGlanceQuickConverter>()];
            inspectorProviders = (IGlanceInspectorProvider[])[.. runtime.Services.GetServices<IGlanceInspectorProvider>()];
            IReadOnlyList<IGlanceApplicationMessageHandler> bridgeHandlers = (IGlanceApplicationMessageHandler[])[.. runtime.Services.GetServices<IGlanceApplicationMessageHandler>()];
            IReadOnlyList<IGlanceActionProvider> actionProviders = (IGlanceActionProvider[])[.. runtime.Services.GetServices<IGlanceActionProvider>()];
            IReadOnlyList<IGlanceIntent> intents = (IGlanceIntent[])[.. runtime.Services.GetServices<IGlanceIntent>()];
            IReadOnlyList<ITranscriptionProvider> transcriptionProviders = (ITranscriptionProvider[])[.. runtime.Services.GetServices<ITranscriptionProvider>()];

            if (components.Count == 0 && assistantProviders.Count == 0 && assistantCommandHandlers.Count == 0 && assistantSemanticResolvers.Count == 0 && quickConverters.Count == 0 && inspectorProviders.Count == 0 && transcriptionProviders.Count == 0)
            {
                throw new InvalidOperationException("The package did not register a Glance component or background capability.");
            }

            if (components.Any(component => preferences.GetComponent(component.Id) is not null))
            {
                throw new InvalidOperationException("The package registered a component identifier that is already loaded.");
            }

            string packageId = GlanceModuleInstallationStore.GetPackageId(result.SourcePath);
            registeredInstallationIds = [.. components.Select(component => component.Id)];
            registeredPackageId = packageId;
            installations.Register(packageId, registeredInstallationIds, () => UninstallAsync(result.SourcePath));
            quickConverterRegistry.Register(packageId, quickConverters);
            quickConvertersRegistered = true;
            await quickConverterPreferences.RegisterAsync(quickConverters.Select(converter => converter.Descriptor.Id));
            inspectorProviderRegistry.Register(packageId, inspectorProviders);
            inspectorProvidersRegistered = true;
            await inspectorProviderPreferences.RegisterAsync(inspectorProviders.Select(provider => provider.Descriptor.Id));

            IServiceProvider moduleServices = runtime.Services;
            runtimeServices.AddModuleProvider(moduleServices);

            foreach (ITranscriptionProvider transcriptionProvider in transcriptionProviders)
            {
                transcriptionRegistrations.Add(transcriptionProviderRegistry.Register(transcriptionProvider));
            }

            if (components.Count > 0)
            {
                await preferences.RegisterComponentsAsync(components, () => (IGlanceModuleSettingViewModel[])[.. moduleServices.GetServices<IGlanceModuleSettingViewModel>()]);
            }

            bridgeRouter.AddHandlers(bridgeHandlers);
            actionService.Register(actionProviders);
            intentService.Register(intents);
            assistantCommandService.Register(assistantCommandHandlers);
            applicationServices.GetRequiredService<GlanceAssistantSemanticResolverService>().Register(assistantSemanticResolvers);
            assistantService.Register(assistantProviders);
            LoadedModulePackage loadedPackage = new(packageId,
                result.SourcePath,
                result.ContentDirectory,
                runtime,
                components,
                assistantProviders,
                assistantCommandHandlers,
                assistantSemanticResolvers,
                quickConverters,
                inspectorProviders,
                bridgeHandlers,
                actionProviders,
                intents,
                transcriptionProviders,
                transcriptionRegistrations);
            loadedPackages.Add(loadedPackage);
            transcriptionRegistrations = [];

            runtime = null;

            lock (synchronization)
            {
                _ = knownPackages.Add(result.SourcePath);
            }

            logger.LogInformation("Loaded Glance module package {ModulePackage} with {ComponentCount} component(s), {AssistantProviderCount} assistant provider(s), {QuickConverterCount} quick converter(s), {InspectorProviderCount} inspector provider(s), and {TranscriptionProviderCount} transcription provider(s)", result.SourcePath, components.Count, assistantProviders.Count, quickConverters.Count, inspectorProviders.Count, transcriptionProviders.Count);
            return ModuleInstallResult.Installed(components.Select(component => component.Id), packageId, quickConverters.Select(converter => converter.Descriptor.Id), inspectorProviders.Select(provider => provider.Descriptor.Id));
        }
        catch (Exception exception)
        {
            DisposeRegistrations(transcriptionRegistrations);

            if (inspectorProvidersRegistered)
            {
                inspectorProviderRegistry.Unregister(inspectorProviders);
                await inspectorProviderPreferences.RemoveAsync(inspectorProviders.Select(provider => provider.Descriptor.Id));
            }

            if (quickConvertersRegistered)
            {
                quickConverterRegistry.Unregister(quickConverters);
                await quickConverterPreferences.RemoveAsync(quickConverters.Select(converter => converter.Descriptor.Id));
            }

            if (registeredPackageId is not null)
            {
                installations.Unregister(registeredPackageId, registeredInstallationIds);
            }

            logger.LogError(exception, "Failed to activate Glance module package {ModulePackage}", result.SourcePath);
            return ModuleInstallResult.Failed(exception.Message);
        }
        finally
        {
            if (runtime is not null)
            {
                await runtime.DisposeAsync();
            }
        }
    }

    private void HandlePackageChanged(object sender, FileSystemEventArgs args) => SchedulePackage(args.FullPath);

    private void HandlePackageRenamed(object sender, RenamedEventArgs args) => SchedulePackage(args.FullPath);

    private void HandleWatcherError(object sender, ErrorEventArgs args)
    {
        if (sender is not FileSystemWatcher watcher)
        {
            return;
        }

        logger.LogWarning(args.GetException(), "The Glance module folder watcher missed one or more changes in {ModuleDirectory}; rescanning the folder", watcher.Path);

        foreach (string packagePath in Directory.EnumerateFiles(watcher.Path, "*.glance", SearchOption.AllDirectories))
        {
            SchedulePackage(packagePath);
        }
    }

    private FileSystemWatcher CreateWatcher(string directory)
    {
        FileSystemWatcher watcher = new(directory, "*.glance")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Created += HandlePackageChanged;
        watcher.Changed += HandlePackageChanged;
        watcher.Renamed += HandlePackageRenamed;
        watcher.Error += HandleWatcherError;
        return watcher;
    }

    private void SchedulePackage(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);

        if (fullPackagePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.StartsWith(".removed-", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        lock (synchronization)
        {
            if (knownPackages.Contains(fullPackagePath))
            {
                return;
            }

            if (pendingPackages.Remove(fullPackagePath, out CancellationTokenSource? previous))
            {
                previous.Cancel();
                previous.Dispose();
            }

            CancellationTokenSource cancellation = new();
            pendingPackages.Add(fullPackagePath, cancellation);
            _ = PrepareAndInstallAsync(fullPackagePath, cancellation);
        }
    }

    private async Task PrepareAndInstallAsync(string packagePath, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(copySettleDelay, cancellation.Token);

            if (!await WaitForStablePackageAsync(packagePath, cancellation.Token))
            {
                return;
            }

            await DispatchAsync(async () =>
            {
                lock (synchronization)
                {
                    if (knownPackages.Contains(packagePath))
                    {
                        return;
                    }
                }

                string installedPackagePath = GlanceModuleInstallationStore.NormalizePackage(packagePath);
                string packageId = GlanceModuleInstallationStore.GetPackageId(installedPackagePath);
                GlanceModuleLoadResult? result = GlanceModuleLoader.LoadPackage(installedPackagePath);

                if (result is not null)
                {
                    if (settings.UninstalledModulePackages.RemoveAll(value => string.Equals(value, packageId, StringComparison.OrdinalIgnoreCase)) > 0)
                    {
                        await settingsWriter.WriteAsync(value => value.UninstalledModulePackages = [.. settings.UninstalledModulePackages]);
                    }

                    await InstallAsync(result);
                    return;
                }

                logger.LogWarning("The discovered Glance module package {ModulePackage} did not contain a loadable module", packagePath);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load the newly discovered Glance module package {ModulePackage}", packagePath);
        }
        finally
        {
            lock (synchronization)
            {
                if (pendingPackages.TryGetValue(packagePath, out CancellationTokenSource? current) && ReferenceEquals(current, cancellation))
                {
                    _ = pendingPackages.Remove(packagePath);
                    cancellation.Dispose();
                }
            }
        }
    }

    private Task<ModuleInstallResult> InstallPackageAsync(string packagePath) => DispatchAsync(async () =>
    {
        if (!File.Exists(packagePath) ||
            !string.Equals(Path.GetExtension(packagePath), ".glance", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleInstallResult.Failed("Choose a valid .glance module package.");
        }

        string packageId = Path.GetFileNameWithoutExtension(packagePath);
        LoadedModulePackage? existingPackage = loadedPackages.FirstOrDefault(candidate =>
            string.Equals(candidate.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
        string expectedPath = Path.Combine(GlanceModuleInstallationStore.RootDirectory, packageId, $"{packageId}.glance");
        string installedPackagePath;

        lock (synchronization)
        {
            _ = knownPackages.Add(expectedPath);

            if (pendingPackages.Remove(expectedPath, out CancellationTokenSource? pending))
            {
                pending.Cancel();
                pending.Dispose();
            }
        }

        try
        {
            string fullSourcePath = Path.GetFullPath(packagePath);
            installedPackagePath = string.Equals(fullSourcePath, expectedPath, StringComparison.OrdinalIgnoreCase)
                ? fullSourcePath
                : GlanceModuleInstallationStore.StagePackage(fullSourcePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            lock (synchronization)
            {
                _ = knownPackages.Remove(expectedPath);
            }

            logger.LogError(exception, "Failed to stage Glance module package {ModulePackage}", packagePath);
            return ModuleInstallResult.Failed(exception.Message);
        }

        if (existingPackage is not null)
        {
            logger.LogInformation("Staged updated Glance module package {ModulePackage}; it will be activated on the next launch", installedPackagePath);
            return ModuleInstallResult.Staged(existingPackage.Components.Select(component => component.Id), existingPackage.PackageId, existingPackage.QuickConverters.Select(converter => converter.Descriptor.Id), existingPackage.InspectorProviders.Select(provider => provider.Descriptor.Id));
        }

        GlanceModuleLoadResult? result;

        try
        {
            result = GlanceModuleLoader.LoadPackage(installedPackagePath);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to inspect Glance module package {ModulePackage}", installedPackagePath);

            lock (synchronization)
            {
                _ = knownPackages.Remove(installedPackagePath);
            }

            GlanceModuleInstallationStore.DeletePackagePayload(installedPackagePath);
            GlanceModuleLoader.RefreshResolutionPaths(loadedPackages.Select(package => package.ContentDirectory));
            return ModuleInstallResult.Failed(exception.Message);
        }

        if (result is null)
        {
            lock (synchronization)
            {
                _ = knownPackages.Remove(installedPackagePath);
            }

            GlanceModuleInstallationStore.DeletePackagePayload(installedPackagePath);
            GlanceModuleLoader.RefreshResolutionPaths(loadedPackages.Select(package => package.ContentDirectory));
            return ModuleInstallResult.Failed("The package does not contain a loadable Glance module.");
        }

        ModuleInstallResult installResult = await InstallAsync(result);

        if (!installResult.IsSuccessful)
        {
            lock (synchronization)
            {
                _ = knownPackages.Remove(installedPackagePath);
            }

            GlanceModuleInstallationStore.DeletePackagePayload(installedPackagePath);
            GlanceModuleLoader.RefreshResolutionPaths(loadedPackages.Select(package => package.ContentDirectory));
            return installResult;
        }

        if (settings.UninstalledModulePackages.RemoveAll(value => string.Equals(value, packageId, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            await settingsWriter.WriteAsync(value => value.UninstalledModulePackages = [.. settings.UninstalledModulePackages]);
        }

        return installResult;
    });

    private async Task<bool> UninstallAsync(string sourcePath)
    {
        LoadedModulePackage? package = loadedPackages.FirstOrDefault(candidate =>
            string.Equals(candidate.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));

        if (package is null)
        {
            return false;
        }

        if (!settings.UninstalledModulePackages.Contains(package.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            settings.UninstalledModulePackages.Add(package.PackageId);
            await settingsWriter.WriteAsync(value => value.UninstalledModulePackages = [.. settings.UninstalledModulePackages]);
        }

        string[] componentIds = [.. package.Components.Select(component => component.Id)];
        installations.Unregister(package.PackageId, componentIds);
        await DispatchAsync(() => preferences.UnregisterComponentsAsync(package.Components));
        bridgeRouter.RemoveHandlers(package.BridgeHandlers);
        actionService.Unregister(package.ActionProviders);
        intentService.Unregister(package.Intents);
        quickConverterRegistry.Unregister(package.QuickConverters);
        await quickConverterPreferences.RemoveAsync(package.QuickConverters.Select(converter => converter.Descriptor.Id));
        inspectorProviderRegistry.Unregister(package.InspectorProviders);
        await inspectorProviderPreferences.RemoveAsync(package.InspectorProviders.Select(provider => provider.Descriptor.Id));
        assistantCommandService.Unregister(package.AssistantCommandHandlers);
        applicationServices.GetRequiredService<GlanceAssistantSemanticResolverService>().Unregister(package.AssistantSemanticResolvers);
        await assistantService.UnregisterAsync(package.AssistantProviders);
        DisposeRegistrations(package.TranscriptionRegistrations);
        runtimeServices.RemoveModuleProvider(package.Runtime.Services);
        await DisposeRuntimeAsync(package.Runtime);
        _ = loadedPackages.Remove(package);
        GlanceModuleLoader.RefreshResolutionPaths(loadedPackages.Select(candidate => candidate.ContentDirectory));

        lock (synchronization)
        {
            _ = knownPackages.Remove(package.SourcePath);
        }

        GlanceModuleInstallationStore.RemovePackageForCurrentProcess(package.SourcePath);
        logger.LogInformation("Uninstalled Glance module package {ModulePackage}", package.SourcePath);
        return true;
    }

    private static async Task<bool> WaitForStablePackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                FileInfo before = new(packagePath);

                if (!before.Exists || before.Length == 0)
                {
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                using (new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                }

                long length = before.Length;
                DateTime lastWriteTimeUtc = before.LastWriteTimeUtc;
                await Task.Delay(250, cancellationToken);
                FileInfo after = new(packagePath);

                if (after.Exists && after.Length == length && after.LastWriteTimeUtc == lastWriteTimeUtc)
                {
                    return true;
                }
            }
            catch (IOException)
            {
                await Task.Delay(250, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        return false;
    }

    private Task DispatchAsync(Func<Task> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("The UI dispatcher queue rejected the module installation."));
        }

        return completion.Task;
    }

    private async Task DisposeRuntimeAsync(GlanceModuleRuntime runtime)
    {
        try
        {
            await DispatchAsync(() => runtime.DisposeAsync().AsTask());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to dispose a Glance module runtime");
        }
    }

    private static void DisposeRegistrations(IEnumerable<IDisposable> registrations)
    {
        foreach (IDisposable registration in registrations.Reverse())
        {
            registration.Dispose();
        }
    }

    private Task<T> DispatchAsync<T>(Func<Task<T>> action,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(priority, async () =>
            {
                try
                {
                    completion.SetResult(await action());
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("The UI dispatcher queue rejected the module installation."));
        }

        return completion.Task;
    }

    private sealed record LoadedModulePackage(string PackageId,
        string SourcePath,
        string ContentDirectory,
        GlanceModuleRuntime Runtime,
        IReadOnlyList<IGlanceComponent> Components,
        IReadOnlyList<IGlanceAssistantProvider> AssistantProviders,
        IReadOnlyList<IGlanceAssistantCommandHandler> AssistantCommandHandlers,
        IReadOnlyList<IGlanceAssistantSemanticResolver> AssistantSemanticResolvers,
        IReadOnlyList<IGlanceQuickConverter> QuickConverters,
        IReadOnlyList<IGlanceInspectorProvider> InspectorProviders,
        IReadOnlyList<IGlanceApplicationMessageHandler> BridgeHandlers,
        IReadOnlyList<IGlanceActionProvider> ActionProviders,
        IReadOnlyList<IGlanceIntent> Intents,
        IReadOnlyList<ITranscriptionProvider> TranscriptionProviders,
        IReadOnlyList<IDisposable> TranscriptionRegistrations);
}
