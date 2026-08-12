using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
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

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
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
            applicationDispatcherQueue,
            (string[])[.. host.Services.GetRequiredService<GlanceSettings>().UninstalledModulePackages],
            host.Services.GetRequiredService<ILogger<App>>());

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
        DispatcherQueue dispatcherQueue,
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
            });

            await EnqueueAsync(dispatcherQueue, () =>
            {
                moduleManager.LoadStartupModules();
                moduleManager.StartWatching();
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize startup modules");
        }
    }

    private static Task EnqueueAsync(DispatcherQueue dispatcherQueue,
        Action callback)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                callback();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The application dispatcher rejected module startup"));
        }

        return completion.Task;
    }
}
