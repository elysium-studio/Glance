using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.UI.WinUI;
using Elysium.Updates.Abstractions;
using Elysium.Updates.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public sealed class UpdateModule :
    IModule
{
    private const string RestartForUpdateArgument = "update=restart";
    private const string DismissUpdateArgument = "update=dismiss";
#if DEBUG
    private const string SimulateUpdateReadyArgument = "--simulate-update-ready";
#endif

    public void Register(IServiceCollection services)
    {
        if (PackageIdentity.IsFullPackage)
        {
            return;
        }

        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        services.AddUpdateController(configuration =>
        {
            configuration.FeedUrl = "https://elysiumstud.io/feeds/glance";
        });

        services.AddSingleton<AppToastNotifier>();
        services.AddSingleton<IUpdateNotificationService, UpdateNotificationService>();

        services.Subscribe<IUpdateController>((provider, controller) =>
        {
            ILogger<UpdateModule> logger = provider.GetRequiredService<ILogger<UpdateModule>>();

            void ShowUpdateReady(string version, bool applyOnExit)
            {
                bool enqueued = dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        IStringLocalizer localizer = provider.GetRequiredService<IStringLocalizer>();

                        ToastContent content = new ToastBuilder()
                            .AddText(localizer.GetString("UpdateReadyToastTitle"))
                            .AddText(localizer.GetString("UpdateReadyToastDownloaded", version))
                            .AddText(localizer.GetString("UpdateReadyToastRestartRequired"))
                            .SetLaunchArgument(RestartForUpdateArgument)
                            .AddButton(localizer.GetString("UpdateReadyToastRestartButton"), RestartForUpdateArgument)
                            .AddButton(localizer.GetString("UpdateReadyToastDismissButton"), DismissUpdateArgument)
                            .Build();

                        provider.GetRequiredService<IUpdateNotificationService>().Show(content, argument =>
                        {
                            if (argument == RestartForUpdateArgument)
                            {
                                bool restartEnqueued = dispatcherQueue.TryEnqueue(() =>
                                {
                                    _ = ExitForUpdateAsync(provider, applyOnExit ? controller : null, logger);
                                });

                                if (!restartEnqueued)
                                {
                                    logger.LogWarning("Dispatcher rejected update restart request");
                                }
                            }
                        });
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Failed to show update-ready notification for version {Version}", version);
                    }
                });

                if (!enqueued)
                {
                    logger.LogWarning("Dispatcher rejected update-ready notification for version {Version}", version);
                }
            }

            void HandleUpdateReady(string version) => ShowUpdateReady(version, true);

            controller.UpdateReady += HandleUpdateReady;

#if DEBUG
            if (Array.Exists(Environment.GetCommandLineArgs(), argument => string.Equals(argument, SimulateUpdateReadyArgument, StringComparison.OrdinalIgnoreCase)))
            {
                ShowUpdateReady("test", false);
            }
#endif

            return () => controller.UpdateReady -= HandleUpdateReady;
        });
    }

    private static async Task ExitForUpdateAsync(IServiceProvider provider, IUpdateController? controller, ILogger logger)
    {
        try
        {
            controller?.ApplyOnExit();
            await provider.GetRequiredService<IApplicationLifetime>().ExitAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to restart for update");
        }
    }
}
