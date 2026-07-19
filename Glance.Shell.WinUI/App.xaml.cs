using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.UI;

namespace Glance.Shell.WinUI;

public static class WinUiColourResourceDebugger
{
    [Conditional("DEBUG")]
    public static void Dump()
    {
        Debug.WriteLine(string.Empty);
        Debug.WriteLine("==================================================");
        Debug.WriteLine("WINUI DEFAULT COLOUR RESOURCES");
        Debug.WriteLine("==================================================");

        DumpDictionary(
            new XamlControlsResources(),
            "WinUI",
            new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));

        Debug.WriteLine(string.Empty);
        Debug.WriteLine("==================================================");
        Debug.WriteLine("APPLICATION COLOUR RESOURCES");
        Debug.WriteLine("==================================================");

        DumpDictionary(
            App.Current.Resources,
            "Application",
            new HashSet<ResourceDictionary>(ReferenceEqualityComparer.Instance));

        Debug.WriteLine("==================================================");
        Debug.WriteLine("END OF COLOUR RESOURCES");
        Debug.WriteLine("==================================================");
        Debug.WriteLine(string.Empty);
    }

    private static void DumpDictionary(
        ResourceDictionary dictionary,
        string path,
        HashSet<ResourceDictionary> visited)
    {
        if (!visited.Add(dictionary))
        {
            return;
        }

        foreach (var resource in dictionary.OrderBy(
                     item => item.Key?.ToString(),
                     StringComparer.OrdinalIgnoreCase))
        {
            WriteResource(path, resource.Key, resource.Value);
        }

        foreach (var theme in dictionary.ThemeDictionaries.OrderBy(
                     item => item.Key?.ToString(),
                     StringComparer.OrdinalIgnoreCase))
        {
            if (theme.Value is ResourceDictionary themeDictionary)
            {
                DumpDictionary(
                    themeDictionary,
                    $"{path}/Theme:{theme.Key}",
                    visited);
            }
        }

        for (var index = 0; index < dictionary.MergedDictionaries.Count; index++)
        {
            var mergedDictionary = dictionary.MergedDictionaries[index];

            var source = mergedDictionary.Source?.ToString();

            var mergedPath = string.IsNullOrWhiteSpace(source)
                ? $"{path}/Merged:{index}"
                : $"{path}/Merged:{index}:{source}";

            DumpDictionary(mergedDictionary, mergedPath, visited);
        }
    }

    private static void WriteResource(
        string path,
        object key,
        object value)
    {
        switch (value)
        {
            case Color color:
                Debug.WriteLine(
                    $"[{path}] {key} = {ToHex(color)}");

                break;

            case SolidColorBrush brush:
                Debug.WriteLine(
                    $"[{path}] {key} = {ToHex(brush.Color)}" +
                    $"{FormatOpacity(brush.Opacity)}");

                break;

            case LinearGradientBrush gradientBrush:
                for (var index = 0; index < gradientBrush.GradientStops.Count; index++)
                {
                    var stop = gradientBrush.GradientStops[index];

                    Debug.WriteLine(
                        $"[{path}] {key}[{index}] = {ToHex(stop.Color)}, Offset={stop.Offset:0.###}" +
                        $"{FormatOpacity(gradientBrush.Opacity)}");
                }

                break;
        }
    }

    private static string ToHex(Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string FormatOpacity(double opacity)
    {
        return Math.Abs(opacity - 1.0) < 0.001
            ? string.Empty
            : $", Opacity={opacity:0.###}";
    }
}

public partial class App
{
    private IHost? host;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiColourResourceDebugger.Dump();

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
