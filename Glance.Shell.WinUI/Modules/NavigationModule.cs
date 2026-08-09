using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;

namespace Glance.Shell.WinUI;

public sealed class NavigationModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddSingleton<IViewModelFactory>(provider => new ViewModelFactory((key, viewModelArgs) =>
            {
                key = key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length]
                    : key;
                ViewDescriptor descriptor = provider.GetRequiredKeyedService<ViewDescriptor>(key);
                Type type = descriptor.ViewModelType!;

                if (descriptor.ViewType is not null &&
                    typeof(Window).IsAssignableFrom(descriptor.ViewType) &&
                    provider.GetRequiredService<WindowRegistry>().TryGetOpen(descriptor.ViewType, out Window? existingWindow) &&
                    existingWindow?.Content is FrameworkElement { DataContext: not null } content)
                {
                    return content.DataContext;
                }

                if (viewModelArgs is { Length: > 0 })
                {
                    IServiceFactory serviceFactory =
                        provider.GetRequiredService<IServiceFactory>();
                    return serviceFactory.Create(type, viewModelArgs);
                }

                return provider.GetRequiredKeyedService(type, key)!;
            }))
            .AddSingleton<IViewFactory>(provider => new ViewFactory((key, viewArgs) =>
            {
                key = key.EndsWith("ViewModel", StringComparison.Ordinal) ? key[..^"ViewModel".Length]
                    : key;

                ViewDescriptor? descriptor = provider.GetKeyedService<ViewDescriptor>(key);
                Type? type = descriptor?.ViewType;

                if (type is null)
                {
                    return null;
                }

                if (typeof(Window).IsAssignableFrom(type) &&
                    provider.GetRequiredService<WindowRegistry>().TryGetOpen(type, out Window? existingWindow))
                {
                    return existingWindow;
                }

                if (viewArgs is { Length: > 0 })
                {
                    IServiceFactory serviceFactory =
                        provider.GetRequiredService<IServiceFactory>();
                    return serviceFactory.Create(type, viewArgs);
                }

                return provider.GetKeyedService(type, key);
            }))
            .AddServiceFactory().AddSingleton<WindowRegistry>().AddKeyedSingleton<INavigationHandler, WindowHandler>(typeof(Window)).AddKeyedSingleton<INavigationHandler, GlanceContentDialogHandler>(typeof(ContentDialog)).AddKeyedSingleton<INavigationHandler, PopupHandler>(typeof(Popup));
}
