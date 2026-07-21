using Elysium.Application;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using System;

namespace Glance.Shell.WinUI;

public sealed class ApplicationModule(
    string applicationData,
    DispatcherQueue dispatcherQueue) :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddSingleton(new AppEnvironment(applicationData)).AddSingleton<IStartupManager>(new StartupManager(Environment.ProcessPath ?? string.Empty, "GlanceDesktop", "GlanceDesktop")).AddSingleton<IDispatcher>(new Dispatcher(args =>
            {
                if (!dispatcherQueue.TryEnqueue(() => args()))
                {
                    throw new InvalidOperationException("The UI dispatcher queue rejected the operation.");
                }
            }));
    }
}
