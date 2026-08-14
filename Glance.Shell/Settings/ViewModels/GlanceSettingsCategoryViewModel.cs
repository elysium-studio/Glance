using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceSettingsCategoryViewModel :
    SettingsCategoryViewModel,
    IRecipient<OptionsChangedEventArgs<GlanceSettings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IGlanceViewModel[] items;
    private readonly IMessenger messenger;
    private bool disposed;
    private GlanceSettings settings;

    public GlanceSettingsCategoryViewModel(string id,
        string title,
        string glyph,
        IMessenger messenger,
        IDispatcher dispatcher,
        GlanceSettings settings,
        IEnumerable<IGlanceViewModel> items) :
        base(id, title, glyph, items.Where(item => IsAvailable(item, settings)).Cast<object>())
    {
        this.messenger = messenger;
        this.dispatcher = dispatcher;
        this.settings = settings;
        this.items = [.. items];
        messenger.Register<OptionsChangedEventArgs<GlanceSettings>>(this);
    }

    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        messenger.UnregisterAll(this);

        foreach (IDisposable item in items.Where(item => !Contains(item)).OfType<IDisposable>())
        {
            item.Dispose();
        }

        base.Dispose();
    }

    public void Receive(OptionsChangedEventArgs<GlanceSettings> message) => dispatcher.Dispatch(() =>
    {
        settings = message.Options;
        SynchronizeItems();
    });

    private void SynchronizeItems()
    {
        int index = 0;

        foreach (IGlanceViewModel item in items.Where(item => IsAvailable(item, settings)))
        {
            int currentIndex = IndexOf(item);

            if (currentIndex < 0)
            {
                Insert(index, item);
            }
            else if (currentIndex != index)
            {
                Move(currentIndex, index);
            }

            index++;
        }

        while (Count > index)
        {
            RemoveAt(Count - 1);
        }
    }

    private static bool IsAvailable(IGlanceViewModel item,
        GlanceSettings settings) => item is not IConditionalGlanceViewModel conditional || conditional.IsAvailable(settings);
}
