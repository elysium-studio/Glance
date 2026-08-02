using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
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

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance");
        DispatcherQueue applicationDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        dispatcherQueue = applicationDispatcherQueue;

        host = Host.CreateDefaultBuilder().UseWritableContentRoot(applicationData).ConfigureServices(services =>
            {
                services
                    .AddSingleton<IApplicationLifetime>(new ApplicationLifetime(ShutdownAsync))
                    .AddApplication().AddPresentation().AddModules(new ApplicationModule(applicationData, applicationDispatcherQueue), new ConfigurationModule(), new LocalizationModule(), new NavigationModule(), new DesktopModule(), new BridgeModule(), new SettingsModule(), new GlanceSettingsModule(), new ModulesSettingsModule(), new WindowsSettingsModule());
            })
            .Build();

        host.Start();

        GlanceRuntimeServiceProvider runtimeServices = new(host.Services);
        ViewExtension.DefaultProvider = runtimeServices;
        ViewModelExtension.DefaultProvider = runtimeServices;

        moduleManager = new GlanceModuleManager(host.Services, runtimeServices, applicationDispatcherQueue, host.Services.GetRequiredService<ILogger<GlanceModuleManager>>());
        moduleManager.LoadStartupModules();

        _ = host.Services.GetRequiredKeyedService<DesktopIslandView>("DesktopIslandView");
        moduleManager.StartWatching();
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

        if (currentModuleManager is not null)
        {
            await currentModuleManager.DisposeAsync();
            moduleManager = null;
        }

        if (currentHost is not null)
        {
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
}
