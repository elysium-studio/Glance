using Glance.Application.Abstractions;
using Glance.Shell;
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
    private readonly HashSet<string> knownPackages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GlanceModuleManager> logger;
    private readonly List<GlanceModuleRuntime> runtimes = [];
    private readonly ModulePreferenceService preferences;
    private readonly IServiceProvider applicationServices;
    private readonly Dictionary<string, CancellationTokenSource> pendingPackages = new(StringComparer.OrdinalIgnoreCase);
    private readonly object synchronization = new();

    public GlanceModuleManager(
        IServiceProvider applicationServices,
        GlanceRuntimeServiceProvider runtimeServices,
        DispatcherQueue dispatcherQueue,
        ILogger<GlanceModuleManager> logger)
    {
        this.applicationServices = applicationServices;
        this.runtimeServices = runtimeServices;
        this.dispatcherQueue = dispatcherQueue;
        this.logger = logger;
        preferences = applicationServices.GetRequiredService<ModulePreferenceService>();

        Directory.CreateDirectory(GlanceModuleLoader.UserModulesDirectory);
        watchers = GlanceModuleLoader.ModuleDirectories
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreateWatcher)
            .ToArray();
    }

    public async Task LoadStartupModulesAsync()
    {
        foreach (GlanceModuleLoadResult result in GlanceModuleLoader.Load())
        {
            await InstallAsync(result);
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
            pending = pendingPackages.Values.ToArray();
            pendingPackages.Clear();
        }

        foreach (CancellationTokenSource cancellation in pending)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        foreach (GlanceModuleRuntime runtime in runtimes.AsEnumerable().Reverse())
        {
            await runtime.DisposeAsync();
        }
    }

    private async Task InstallAsync(GlanceModuleLoadResult result)
    {
        GlanceModuleRuntime? runtime = null;

        try
        {
            runtime = await GlanceModuleRuntime.CreateAsync(applicationServices, result.Modules);
            IReadOnlyList<IGlanceComponent> components = runtime.Services.GetServices<IGlanceComponent>().ToArray();

            if (components.Count == 0)
            {
                throw new InvalidOperationException("The package did not register a Glance component.");
            }

            if (components.Any(component => preferences.GetComponent(component.Id) is not null))
            {
                throw new InvalidOperationException("The package registered a component identifier that is already loaded.");
            }

            IServiceProvider moduleServices = runtime.Services;
            runtimeServices.AddModuleProvider(moduleServices);
            await preferences.RegisterComponentsAsync(components, () => moduleServices.GetServices<IGlanceModuleSettingViewModel>().ToArray());
            runtimes.Add(runtime);
            runtime = null;

            lock (synchronization)
            {
                knownPackages.Add(result.SourcePath);
            }

            logger.LogInformation("Loaded Glance module package {ModulePackage} with {ComponentCount} component(s)", result.SourcePath, components.Count);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to activate Glance module package {ModulePackage}", result.SourcePath);
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

    private async Task PrepareAndInstallAsync(
        string packagePath,
        CancellationTokenSource cancellation)
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
                GlanceModuleLoadResult? result = GlanceModuleLoader.LoadPackage(packagePath);

                if (result is not null)
                {
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
                    pendingPackages.Remove(packagePath);
                    cancellation.Dispose();
                }
            }
        }
    }

    private static async Task<bool> WaitForStablePackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
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
}
