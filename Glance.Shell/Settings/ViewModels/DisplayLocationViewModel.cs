using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public sealed partial class DisplayLocationViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, int> read,
    Action<GlanceSettings, int> write) :
    ObservableReadWriteViewModel<GlanceSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, read, write),
    IGlanceViewModel
{
    protected override void ValueChanged(int value)
    {
        settings.DisplayLocation = (GlanceDisplayLocation)value;
        messenger.Send(new OptionsChangedEventArgs<GlanceSettings>(settings));
        base.ValueChanged(value);
    }
}
