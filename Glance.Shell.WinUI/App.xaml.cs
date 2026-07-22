using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.UI.WinUI;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace Glance.Shell.WinUI;

public sealed partial class App
{
    private IHost? host;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance");
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        host = Host.CreateDefaultBuilder().UseWritableContentRoot(applicationData).ConfigureServices(services =>
            {
                services
                    .AddApplication().AddPresentation().AddModules(new ApplicationModule(applicationData, dispatcherQueue), new ConfigurationModule(), new LocalizationModule(), new NavigationModule(), new DesktopModule(), new SettingsModule(), new GlanceSettingsModule(), new ModulesSettingsModule(), new WindowsSettingsModule());

                foreach (IGlanceModule module in GlanceModuleLoader.Load())
                {
                    module.Register(services);
                }
            })
            .Build();

        ViewExtension.DefaultProvider = host.Services;
        ViewModelExtension.DefaultProvider = host.Services;

        _ = host.Services.GetRequiredKeyedService<DesktopIslandView>("DesktopIslandView");

        host.Start();
    }
}
