using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IApplicationLifetime = Elysium.Application.Abstractions.IApplicationLifetime;

namespace Glance.Shell.WinUI;

public sealed partial class App
{
    private readonly Lock shutdownLock = new();

    private DispatcherQueue? dispatcherQueue;
    private IHost? host;
    private GlanceModuleManager? moduleManager;
    private Task? shutdownTask;
    private Task? startupModulesTask;
    private Task? startupNavigationTask;
    private readonly AppInstance appInstance;
    private readonly AppActivationArguments initialActivation;

    public App(AppInstance appInstance, AppActivationArguments initialActivation)
    {
        this.appInstance = appInstance;
        this.initialActivation = initialActivation;
        appInstance.Activated += HandleAppActivated;
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance");
        GlanceModuleDataMigration.Migrate(applicationData);
        DispatcherQueue applicationDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        dispatcherQueue = applicationDispatcherQueue;

        host = Host.CreateDefaultBuilder().UseWritableContentRoot(applicationData).ConfigureServices(services => _ = services
                    .AddSingleton<IApplicationLifetime>(new ApplicationLifetime(ShutdownAsync))
                    .AddApplication().AddPresentation().AddModules(new ApplicationModule(applicationData, applicationDispatcherQueue), new ConfigurationModule(), new LocalizationModule(), new NavigationModule(), new DesktopModule(), new BridgeModule(), new SettingsModule(), new GlanceSettingsModule(), new ModulesSettingsModule(), new SetupTourModule(), new WindowsSettingsModule()))
            .Build();

        host.Start();

        GlanceRuntimeServiceProvider runtimeServices = new(host.Services);
        ViewExtension.DefaultProvider = runtimeServices;
        ViewModelExtension.DefaultProvider = runtimeServices;

        moduleManager = new GlanceModuleManager(host.Services, runtimeServices, applicationDispatcherQueue, host.Services.GetRequiredService<ILogger<GlanceModuleManager>>());
        _ = host.Services.GetRequiredKeyedService<DesktopIslandView>("DesktopIslandView");
        startupModulesTask = InitializeStartupModulesAsync(moduleManager,
            (string[])[.. host.Services.GetRequiredService<GlanceSettings>().UninstalledModulePackages],
            host.Services.GetRequiredService<ILogger<App>>());
        _ = RouteActivationAsync(initialActivation);

        if (host.Services.GetRequiredService<GlanceSettings>().ShowSetupOnStartup)
        {
            startupNavigationTask = NavigateToStartupTourAsync(host.Services.GetRequiredService<INavigator>(),
                host.Services.GetRequiredService<ILogger<App>>());
        }
    }

    private Task ShutdownAsync()
    {
        lock (shutdownLock)
        {
            return shutdownTask ??= ShutdownCoreAsync();
        }
    }

    private async Task ShutdownCoreAsync()
    {
        appInstance.Activated -= HandleAppActivated;
        GlanceModuleManager? currentModuleManager = moduleManager;
        IHost? currentHost = host;

        if (startupModulesTask is not null)
        {
            await startupModulesTask;
            startupModulesTask = null;
        }

        if (currentModuleManager is not null)
        {
            await currentModuleManager.DisposeAsync();
            moduleManager = null;
        }

        if (currentHost is not null)
        {
            if (startupNavigationTask is not null)
            {
                await startupNavigationTask;
            }

            await currentHost.StopAsync();
            await CompleteShutdownAsync(currentHost);
            return;
        }

        Current.Exit();
    }

    private void HandleAppActivated(object? sender, AppActivationArguments args)
    {
        DispatcherQueue? queue = dispatcherQueue;
        if (queue is null) return;
        _ = queue.TryEnqueue(() => _ = RouteActivationAsync(args));
    }

    private async Task RouteActivationAsync(AppActivationArguments activation)
    {
        try
        {
            if (startupModulesTask is not null) await startupModulesTask;
            IHost? currentHost = host;
            if (currentHost is null) return;
            GlanceContentContext? context = activation.Kind switch
            {
                ExtendedActivationKind.File when activation.Data is IFileActivatedEventArgs fileArgs => new GlanceContentContext(
                    GlanceContentKind.FilesAndFolders,
                    (GlanceStorageItem[])[.. fileArgs.Files.OfType<IStorageItem>().Select(item => new GlanceStorageItem(item.Path, item.Name, item is StorageFolder))]),
                ExtendedActivationKind.Protocol when activation.Data is IProtocolActivatedEventArgs protocolArgs => new GlanceContentContext(
                    GlanceContentKind.WebLink, [], protocolArgs.Uri.AbsoluteUri),
                _ => null
            };
            if (context is null) return;
            IGlanceIntentService intents = currentHost.Services.GetRequiredService<IGlanceIntentService>();
            GlanceIntentDescriptor? intent = intents.GetIntents(context).FirstOrDefault();
            if (intent is not null) _ = await intents.InvokeAsync(intent.Id, context);
        }
        catch (Exception exception)
        {
            host?.Services.GetService<ILogger<App>>()?.LogError(exception, "Failed to route application activation");
        }
    }

    private Task CompleteShutdownAsync(IHost currentHost)
    {
        DispatcherQueue currentDispatcherQueue = dispatcherQueue
            ?? throw new InvalidOperationException("The application dispatcher is not available");

        if (currentDispatcherQueue.HasThreadAccess)
        {
            CompleteShutdown(currentHost);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!currentDispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                CompleteShutdown(currentHost);
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The application dispatcher rejected the shutdown request"));
        }

        return completion.Task;
    }

    private void CompleteShutdown(IHost currentHost)
    {
        currentHost.Dispose();
        host = null;
        Current.Exit();
    }

    private static async Task NavigateToStartupTourAsync(INavigator navigator,
        ILogger logger)
    {
        try
        {
            await navigator.NavigateAsync("SetupTourWindow");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to navigate to the startup setup tour");
        }
    }

    private static async Task InitializeStartupModulesAsync(GlanceModuleManager moduleManager,
        IReadOnlyList<string> suppressedPackageIds,
        ILogger logger)
    {
        try
        {
            await Task.Run(() =>
            {
                GlanceModuleInstallationStore.PrepareForStartup();
                GlanceModuleInstallationStore.RemoveSuppressedPackages(suppressedPackageIds);
                GlanceModuleLoader.Initialize();
            }).ConfigureAwait(false);

            await moduleManager.LoadStartupModulesAsync().ConfigureAwait(false);
            moduleManager.StartWatching();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize startup modules");
        }
    }

}
