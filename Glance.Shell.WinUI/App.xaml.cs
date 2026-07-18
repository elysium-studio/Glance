using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Glance.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace Glance.Shell.WinUI;

public partial class App
{
    private IHost? host;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddSingleton<IViewModelFactory>(provider => new ViewModelFactory((key, args) =>
                    provider.GetRequiredKeyedService<DesktopIslandViewModel>(key)))
                .AddServiceFactory()
                .AddViewFor(
                    ServiceLifetime.Singleton,
                    provider => new DesktopIslandView(),
                    provider => new DesktopIslandViewModel()))
            .Build();

        ViewModelExtension.DefaultProvider = host.Services;

        host.Start();

        _ = host.Services.GetRequiredKeyedService<DesktopIslandView>("DesktopIslandView");
    }
}
