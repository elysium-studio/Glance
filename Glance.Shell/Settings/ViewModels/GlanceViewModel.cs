using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class GlanceViewModel :
    ObservableCollectionViewModel<ISettingViewModel>,
    ISettingViewModel
{
    public GlanceViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        GlanceSettings settings,
        ITextLocalizer localizer,
        IEnumerable<IGlanceViewModel> items) :
        base(provider, factory, messenger, disposer)
    {
        Title = localizer.GetText("GlanceSectionTitle/Text");
        ILookup<string, IGlanceViewModel> categories = items.ToLookup(item => item.SettingsCategory, StringComparer.OrdinalIgnoreCase);

        AddCategory(GlanceSettingsCategories.AppearanceAndBehaviour,
            localizer.GetText("AppearanceAndBehaviourSettingsTitle"),
            "\uE790",
            messenger,
            dispatcher,
            settings,
            categories);
        AddCategory(GlanceSettingsCategories.SpeechAndCommands,
            localizer.GetText("SpeechAndCommandsSettingsTitle"),
            "\uE720",
            messenger,
            dispatcher,
            settings,
            categories);

        foreach (IGrouping<string, IGlanceViewModel> category in categories.Where(category =>
            !string.Equals(category.Key, GlanceSettingsCategories.AppearanceAndBehaviour, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(category.Key, GlanceSettingsCategories.SpeechAndCommands, StringComparison.OrdinalIgnoreCase)))
        {
            Add(new GlanceSettingsCategoryViewModel(category.Key,
                category.Key,
                "\uE8B7",
                messenger,
                dispatcher,
                settings,
                category));
        }
    }

    public IReadOnlyList<ISettingViewModel> Children => [.. this];

    public string Glyph => "\uE713";

    public string Title { get; }

    private void AddCategory(string id,
        string title,
        string glyph,
        IMessenger messenger,
        IDispatcher dispatcher,
        GlanceSettings settings,
        ILookup<string, IGlanceViewModel> categories)
    {
        IGlanceViewModel[] items = [.. categories[id]];

        if (items.Length > 0)
        {
            Add(new GlanceSettingsCategoryViewModel(id, title, glyph, messenger, dispatcher, settings, items));
        }
    }
}
