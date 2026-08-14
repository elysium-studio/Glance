using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Glance.Shell.WinUI;

public sealed class SettingsModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddTransient<AboutViewModel>()
            .AddViewFor(ServiceLifetime.Transient,
                provider => new SettingsWindow(provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<ITextLocalizer>(),
                    provider.GetRequiredService<IApplicationLifetime>(),
                    provider.GetRequiredService<AboutViewModel>()),
                provider => new SettingsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<ISettingViewModel>>()))
            .AddViewFor<GlanceView, ISettingViewModel, GlanceViewModel>(ServiceLifetime.Transient,
                provider => new GlanceView(),
                provider => new GlanceViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<ITextLocalizer>(),
                    provider.GetRequiredService<IEnumerable<IGlanceViewModel>>()))
            .AddViewFor<ModulesView, ISettingViewModel, ModulesViewModel>(ServiceLifetime.Transient,
                provider => new ModulesView(),
                provider => new ModulesViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<ModulePreferenceService>(),
                    provider.GetRequiredService<ModuleInstallationService>(),
                    provider.GetRequiredService<IApplicationRestartService>(),
                    provider.GetRequiredService<ITextLocalizer>(),
                    provider.GetRequiredService<IEnumerable<IGlanceModuleSettingViewModel>>()))
            .AddViewFor<WindowsView, ISettingViewModel, WindowsViewModel>(ServiceLifetime.Transient,
                provider => new WindowsView(),
                provider => new WindowsViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<ITextLocalizer>(),
                    provider.GetRequiredService<IEnumerable<IWindowsViewModel>>()));
}
