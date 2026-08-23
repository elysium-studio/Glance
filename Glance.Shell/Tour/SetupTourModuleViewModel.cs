using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed partial class SetupTourModuleViewModel :
    ObservableObject
{
    private readonly IDispatcher dispatcher;
    private readonly Func<SetupTourModuleViewModel, Task<bool>> install;
    private readonly Func<SetupTourModuleViewModel, Task<bool>> remove;
    private IGlanceComponent? component;

    public SetupTourModuleViewModel(string id, string displayName, string description, string categoryId, string categoryDisplayName, string categoryGlyph, int categoryOrder, string glyph, string glyphFontFamily, string accentResourceKey, object? accentResourceSource, IGlanceComponent? component, bool isInstalled, IDispatcher dispatcher, Func<SetupTourModuleViewModel, Task<bool>> install, Func<SetupTourModuleViewModel, Task<bool>> remove)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        CategoryId = categoryId;
        CategoryDisplayName = categoryDisplayName;
        CategoryGlyph = categoryGlyph;
        CategoryOrder = categoryOrder;
        Glyph = glyph;
        GlyphFontFamily = glyphFontFamily;
        AccentResourceKey = accentResourceKey;
        AccentResourceSource = accentResourceSource;
        this.component = component;
        IsInstalled = isInstalled;
        this.dispatcher = dispatcher;
        this.install = install;
        this.remove = remove;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string CategoryId { get; }

    public string CategoryDisplayName { get; }

    public string CategoryGlyph { get; }

    public int CategoryOrder { get; }

    public string Glyph { get; private set; }

    public string GlyphFontFamily { get; private set; }

    public string AccentResourceKey { get; private set; }

    public object? AccentResourceSource { get; private set; }

    public GlanceModuleFeedIcon? Icon => FeedItem?.Icon;

    public GlanceModuleFeedItem? FeedItem { get; private set; }

    public bool CanAdd => !IsInstalled && IsAvailable && !IsBusy;

    public bool CanRemove => IsInstalled && !IsBusy;

    public bool ShowAddAction => !IsInstalled;

    public bool ShowRemoveAction => IsInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    [NotifyPropertyChangedFor(nameof(ShowAddAction))]
    [NotifyPropertyChangedFor(nameof(ShowRemoveAction))]
    private bool isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private bool isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(CanRemove))]
    private bool isBusy;

    public void SetFeedItem(GlanceModuleFeedItem feedItem, bool isAvailable)
    {
        FeedItem = feedItem;
        IsAvailable = isAvailable && feedItem.IsCompatible;
        OnPropertyChanged(nameof(Icon));

        if (component is null)
        {
            ApplyVisuals(feedItem.Icon.Type == GlanceModuleIconType.Glyph ? feedItem.Icon.Source : "\uE8B7", string.IsNullOrWhiteSpace(feedItem.Icon.FontFamily) ? "Segoe Fluent Icons" : feedItem.Icon.FontFamily, "AccentTextFillColorPrimaryBrush", null);
        }
    }

    public object? CreateIcon(bool isLightTheme) => component?.CreateIcon(isLightTheme);

    public void SetComponent(IGlanceComponent? value)
    {
        component = value;

        if (value is not null)
        {
            ApplyVisuals(value.IconGlyph, value.IconFontFamily, value.AccentResourceKey, value.CompactContent);
            IsInstalled = true;
            return;
        }

        if (FeedItem is not null)
        {
            ApplyVisuals(FeedItem.Icon.Type == GlanceModuleIconType.Glyph ? FeedItem.Icon.Source : "\uE8B7", string.IsNullOrWhiteSpace(FeedItem.Icon.FontFamily) ? "Segoe Fluent Icons" : FeedItem.Icon.FontFamily, "AccentTextFillColorPrimaryBrush", null);
        }

        IsInstalled = false;
    }

    public async Task InstallAsync()
    {
        if (!CanAdd)
        {
            return;
        }

        IsBusy = true;

        try
        {
            bool installed = await install(this);
            dispatcher.Dispatch(() =>
            {
                if (installed)
                {
                    IsInstalled = true;
                }
            });
        }
        finally
        {
            dispatcher.Dispatch(() => IsBusy = false);
        }
    }

    public async Task RemoveAsync()
    {
        if (!CanRemove)
        {
            return;
        }

        IsBusy = true;

        try
        {
            bool removed = await remove(this);
            dispatcher.Dispatch(() =>
            {
                if (removed)
                {
                    IsInstalled = false;
                }
            });
        }
        finally
        {
            dispatcher.Dispatch(() => IsBusy = false);
        }
    }

    private void ApplyVisuals(string glyph, string glyphFontFamily, string accentResourceKey, object? accentResourceSource)
    {
        Glyph = glyph;
        GlyphFontFamily = glyphFontFamily;
        AccentResourceKey = accentResourceKey;
        AccentResourceSource = accentResourceSource;
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(GlyphFontFamily));
        OnPropertyChanged(nameof(AccentResourceKey));
        OnPropertyChanged(nameof(AccentResourceSource));
    }
}
