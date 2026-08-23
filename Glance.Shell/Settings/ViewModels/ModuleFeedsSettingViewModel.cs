using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed partial class ModuleFeedsSettingViewModel :
    ObservableObject,
    IGlanceViewModel
{
    private readonly IDispatcher dispatcher;
    private readonly IGlanceModuleFeedService feed;
    private readonly IGlanceModuleFeedSourceProvider sourceProvider;
    private readonly ITextLocalizer localizer;
    private readonly INavigator navigator;
    private readonly GlanceSettings settings;
    private readonly IWritableOptions<GlanceSettings> writer;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private bool disposed;

    public ModuleFeedsSettingViewModel(GlanceSettings settings, IWritableOptions<GlanceSettings> writer, IGlanceModuleFeedSourceProvider sourceProvider, IGlanceModuleFeedService feed, IDispatcher dispatcher, ITextLocalizer localizer, INavigator navigator)
    {
        this.settings = settings;
        this.writer = writer;
        this.sourceProvider = sourceProvider;
        this.feed = feed;
        this.dispatcher = dispatcher;
        this.localizer = localizer;
        this.navigator = navigator;
        Feeds = [];
        Rebuild();
        feed.FeedChanged += HandleFeedChanged;
    }

    public ObservableCollection<ModuleFeedSettingItemViewModel> Feeds { get; }

    public string SettingsCategory => GlanceSettingsCategories.ModuleFeeds;

    public bool CanAdd => Uri.TryCreate(NewFeedUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps && !sourceProvider.GetSources().Any(source => Uri.Compare(source.Uri, uri, UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

    public bool HasAddressValidationError => !string.IsNullOrWhiteSpace(NewFeedUrl) && !CanAdd;

    public string AddressValidationMessage => localizer.GetText("ModuleFeedAddressInvalidMessage");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(HasAddressValidationError))]
    private string newFeedUrl = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public void PrepareToAdd()
    {
        NewFeedUrl = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    public Task ShowAddDialogAsync(object xamlRoot)
    {
        PrepareToAdd();
        NavigationParameters parameters = new();
        parameters.Set("XamlRoot", xamlRoot);
        return navigator.NavigateAsync(SettingsNavigationRoutes.AddModuleFeedDialog, [this], parameters);
    }

    public async Task<bool> AddAsync()
    {
        if (!CanAdd || !Uri.TryCreate(NewFeedUrl, UriKind.Absolute, out Uri? uri))
        {
            HasError = true;
            ErrorMessage = localizer.GetText("ModuleFeedAddressInvalidMessage");
            return false;
        }

        GlanceModuleFeedPreference preference = new() { Id = $"custom-{Guid.NewGuid():N}", DisplayName = uri.Host, Url = uri.AbsoluteUri, IsEnabled = true, IsBuiltIn = false };

        bool added = await ApplyAsync(() => settings.ModuleFeeds.Add(preference), () => settings.ModuleFeeds.Remove(preference));

        if (added)
        {
            dispatcher.Dispatch(() => NewFeedUrl = string.Empty);
        }

        return added;
    }

    public async Task RemoveAsync(ModuleFeedSettingItemViewModel item)
    {
        if (!item.CanRemove)
        {
            return;
        }

        GlanceModuleFeedPreference[] removed = [.. settings.ModuleFeeds.Where(preference => string.Equals(preference.Id, item.Id, StringComparison.OrdinalIgnoreCase))];
        await ApplyAsync(() => settings.ModuleFeeds.RemoveAll(preference => string.Equals(preference.Id, item.Id, StringComparison.OrdinalIgnoreCase)), () => settings.ModuleFeeds.AddRange(removed));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        feed.FeedChanged -= HandleFeedChanged;
    }

    private async Task SetEnabledAsync(ModuleFeedSettingItemViewModel item, bool isEnabled)
    {
        GlanceModuleFeedPreference? preference = settings.ModuleFeeds.LastOrDefault(preference => string.Equals(preference.Id, item.Id, StringComparison.OrdinalIgnoreCase));

        if (preference is null)
        {
            preference = new GlanceModuleFeedPreference { Id = item.Id, DisplayName = item.DisplayName, Url = item.Url, IsEnabled = isEnabled, IsBuiltIn = item.IsBuiltIn };
            await ApplyAsync(() => settings.ModuleFeeds.Add(preference), () => settings.ModuleFeeds.Remove(preference));
        }
        else
        {
            bool previous = preference.IsEnabled;
            await ApplyAsync(() => preference.IsEnabled = isEnabled, () => preference.IsEnabled = previous);
        }
    }

    private async Task<bool> ApplyAsync(Action update, Action rollback)
    {
        await synchronization.WaitAsync();

        try
        {
            update();
            GlanceModuleFeedPreference[] snapshot = [.. settings.ModuleFeeds.Select(Clone)];
            await writer.WriteAsync(value => value.ModuleFeeds = [.. snapshot.Select(Clone)]);
            await feed.RefreshAsync();
            dispatcher.Dispatch(() =>
            {
                HasError = false;
                ErrorMessage = string.Empty;
            });
            return true;
        }
        catch
        {
            rollback();
            dispatcher.Dispatch(() =>
            {
                HasError = true;
                ErrorMessage = localizer.GetText("ModuleFeedsUnavailableMessage");
            });
            return false;
        }
        finally
        {
            _ = synchronization.Release();

            if (!disposed)
            {
                dispatcher.Dispatch(Rebuild);
            }
        }
    }

    private void HandleFeedChanged(object? sender, EventArgs args) => dispatcher.Dispatch(Rebuild);

    private void Rebuild()
    {
        Feeds.Clear();

        foreach (GlanceModuleFeedSource source in sourceProvider.GetSources())
        {
            GlanceModuleFeedStatus? status = feed.Sources.FirstOrDefault(status => string.Equals(status.Source.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            Feeds.Add(new ModuleFeedSettingItemViewModel(source, status, SetEnabledAsync));
        }
    }

    private static GlanceModuleFeedPreference Clone(GlanceModuleFeedPreference preference) => new() { Id = preference.Id, DisplayName = preference.DisplayName, Url = preference.Url, IsEnabled = preference.IsEnabled, IsBuiltIn = preference.IsBuiltIn };
}
