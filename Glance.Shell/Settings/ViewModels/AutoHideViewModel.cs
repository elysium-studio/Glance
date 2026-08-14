using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class AutoHideViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, bool> read,
    Action<GlanceSettings, bool> write) :
    GlanceSettingsViewModel<bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IConditionalGlanceViewModel
{
    public bool IsAvailable(GlanceSettings settings) => settings.DisplayLocation == GlanceDisplayLocation.DesktopEdge;
}
