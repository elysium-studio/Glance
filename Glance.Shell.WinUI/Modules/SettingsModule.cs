using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Glance.Shell.WinUI;

public sealed class SettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor(
                ServiceLifetime.Transient,
                provider => new SettingsWindow(),
                provider => new SettingsViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<ISettingViewModel>>()))
            .AddViewFor<GlanceView, ISettingViewModel, GlanceViewModel>(
                ServiceLifetime.Transient,
                provider => new GlanceView(),
                provider => new GlanceViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IGlanceViewModel>>()))
            .AddViewFor<ModulesView, ISettingViewModel, ModulesViewModel>(
                ServiceLifetime.Transient,
                provider => new ModulesView(),
                provider => new ModulesViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IModulesViewModel>>()))
            .AddViewFor<WindowsView, ISettingViewModel, WindowsViewModel>(
                ServiceLifetime.Transient,
                provider => new WindowsView(),
                provider => new WindowsViewModel(
                    provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IEnumerable<IWindowsViewModel>>()));
    }
}
