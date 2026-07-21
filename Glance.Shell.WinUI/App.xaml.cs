using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;
using System;
using System.IO;
using System.Text.Json;

namespace Glance.Shell.WinUI;

public partial class App
{
    private IHost? host;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string applicationData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Glance");
        string settingsPath = Path.Combine(applicationData, "settings.dat");

        Directory.CreateDirectory(applicationData);

        if (!File.Exists(settingsPath))
        {
            File.WriteAllText(settingsPath, "{}");
        }

        host = Host.CreateDefaultBuilder()
            .UseWritableContentRoot(applicationData)
            .ConfigureServices(services =>
            {
                new LocalizationModule().Register(services);

                WritableOptionsBuilder<GlanceSettings> settingsBuilder =
                    new(services, "Settings", "settings.dat");

                settingsBuilder
                    .WithJsonOptions(new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        TypeInfoResolverChain = { GlanceJsonContext.Default }
                    })
                    .UseJson();

                services
                    .AddSingleton<IGlanceAttentionService, GlanceAttentionService>()
                    .AddSingleton<ModulePreferenceService>()
                    .AddSingleton<ISettingsLauncher, SettingsWindowLauncher>()
                    .AddSingleton<IViewModelFactory>(provider => new ViewModelFactory((key, args) =>
                        key switch
                        {
                            "DesktopIslandView" => provider.GetRequiredKeyedService<DesktopIslandViewModel>(key),
                            "SettingsWindow" => provider.GetRequiredKeyedService<SettingsViewModel>(key),
                            _ => throw new InvalidOperationException($"Unknown view-model key '{key}'.")
                        }))
                    .AddServiceFactory()
                    .AddViewFor(
                        ServiceLifetime.Singleton,
                        provider => new DesktopIslandView(),
                        provider => new DesktopIslandViewModel(
                            provider.GetRequiredService<ModulePreferenceService>(),
                            provider.GetRequiredService<IGlanceAttentionService>(),
                            provider.GetRequiredService<ISettingsLauncher>()))
                    .AddViewFor(
                        ServiceLifetime.Transient,
                        provider => new SettingsWindow(),
                        provider => new SettingsViewModel(
                            provider.GetRequiredService<ModulePreferenceService>()));

                foreach (IGlanceModule module in GlanceModuleLoader.Load())
                {
                    module.Register(services);
                }
            })
            .Build();

        ViewExtension.DefaultProvider = host.Services;
        ViewModelExtension.DefaultProvider = host.Services;

        host.Start();

        _ = host.Services.GetRequiredKeyedService<DesktopIslandView>("DesktopIslandView");
    }
}
