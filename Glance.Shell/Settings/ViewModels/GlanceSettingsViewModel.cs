using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public partial class GlanceSettingsViewModel<TValue>(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer,
    Func<GlanceSettings, TValue?> read,
    Action<GlanceSettings, TValue?> write) :
    ObservableReadWriteViewModel<GlanceSettings, TValue>(provider,
        factory,
        messenger,
        disposer,
        dispatcher,
        settings,
        writer,
        read,
        write)
{
    private GlanceSettings settings = settings;

    public override void Receive(OptionsChangedEventArgs<GlanceSettings> message)
    {
        settings = message.Options;
        base.Receive(message);
    }

    protected override void ValueChanged(TValue? value)
    {
        if (IsActive && !EqualityComparer<TValue?>.Default.Equals(Read(settings), value))
        {
            Write(settings, value);
            Messenger.Send(new OptionsChangedEventArgs<GlanceSettings>(settings));
        }

        base.ValueChanged(value);
    }
}
