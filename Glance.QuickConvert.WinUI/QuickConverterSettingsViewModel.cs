using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.QuickConvert.WinUI;

public sealed partial class QuickConverterSettingsViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    private readonly IDispatcher dispatcher;
    private readonly IGlanceQuickConverterManager manager;
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer;
    private readonly INavigator navigator;
    private int disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusOpen))]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStatusError { get; set; }

    public QuickConverterSettingsViewModel(IGlanceQuickConverterManager manager, IDispatcher dispatcher, ModuleResourceTextLocalizer<QuickConvertModule> localizer, INavigator navigator)
    {
        this.manager = manager;
        this.dispatcher = dispatcher;
        this.localizer = localizer;
        this.navigator = navigator;
        manager.Changed += HandleManagerChanged;
        SynchronizeConverters();
    }

    public string ModuleId => "QuickConvert";

    public int Order => 10;

    public ObservableCollection<QuickConverterSettingItemViewModel> Converters { get; } = [];

    public bool CanAdd => !IsBusy;

    public bool IsStatusOpen => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool ShowEmptyState => Converters.Count == 0;

    public string RemoveDialogTitle => localizer.GetText("QuickConverterRemoveDialogTitle");

    public string RemoveDialogMessage => localizer.GetText("QuickConverterRemoveDialogMessage");

    public string RemoveDialogPrimaryButtonText => localizer.GetText("QuickConverterRemoveDialogPrimaryButton");

    public string RemoveDialogCloseButtonText => localizer.GetText("QuickConverterRemoveDialogCloseButton");

    public async Task<bool> ConfirmRemoveAsync(object xamlRoot)
    {
        NavigationParameters parameters = new();
        parameters.Set("XamlRoot", xamlRoot);
        NavigationDialogResult result = await navigator.NavigateAsync<NavigationDialogResult>(nameof(QuickConverterRemoveDialog), [RemoveDialogTitle, RemoveDialogMessage, RemoveDialogPrimaryButtonText, RemoveDialogCloseButtonText], parameters);
        return result == NavigationDialogResult.Primary;
    }

    public async Task<bool> SetEnabledAsync(QuickConverterSettingItemViewModel converter, bool enabled)
    {
        try
        {
            await manager.SetEnabledAsync(converter.Id, enabled);
            return true;
        }
        catch
        {
            ShowStatus(localizer.GetText("QuickConverterChangeFailed"), true);
            return false;
        }
    }

    public async Task InstallAsync(string packagePath)
    {
        if (!CanAdd)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            GlanceQuickConverterInstallResult result = await manager.InstallAsync(packagePath);

            if (!result.IsSuccessful)
            {
                ShowStatus(string.Equals(result.ErrorMessage, "IncompatibleConverterPackage", StringComparison.Ordinal)
                    ? localizer.GetText("QuickConverterPackageIncompatible")
                    : localizer.GetText("QuickConverterInstallFailed"), true);
                return;
            }

            ShowStatus(localizer.GetText(result.RequiresRestart ? "QuickConverterUpdateReady" : "QuickConverterInstalled"), false);
        }
        catch
        {
            ShowStatus(localizer.GetText("QuickConverterInstallFailed"), true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveAsync(QuickConverterSettingItemViewModel converter)
    {
        if (!converter.CanRemove)
        {
            return;
        }

        try
        {
            if (!await manager.RemoveAsync(converter.Id))
            {
                ShowStatus(localizer.GetText("QuickConverterRemoveFailed"), true);
            }
        }
        catch
        {
            ShowStatus(localizer.GetText("QuickConverterRemoveFailed"), true);
        }
    }

    public void Dispose()
    {
        _ = Interlocked.Exchange(ref disposed, 1);
        manager.Changed -= HandleManagerChanged;
    }

    private void HandleManagerChanged(object? sender, EventArgs args) => dispatcher.Dispatch(() =>
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            SynchronizeConverters();
        }
    });

    private void SynchronizeConverters()
    {
        IReadOnlyList<GlanceQuickConverterExtension> extensions = manager.GetConverters();

        if (Converters.Select(converter => converter.Id).SequenceEqual(extensions.Select(extension => extension.Id), StringComparer.OrdinalIgnoreCase))
        {
            foreach (GlanceQuickConverterExtension extension in extensions)
            {
                QuickConverterSettingItemViewModel converter = Converters.First(item => string.Equals(item.Id, extension.Id, StringComparison.OrdinalIgnoreCase));
                converter.SynchronizeEnabled(extension.IsEnabled);
            }

            return;
        }

        Converters.Clear();

        foreach (GlanceQuickConverterExtension extension in extensions)
        {
            Converters.Add(new QuickConverterSettingItemViewModel(extension.Id, extension.DisplayName, extension.Description, extension.IsEnabled, extension.CanRemove, SetEnabledAsync));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void ShowStatus(string message, bool isError)
    {
        IsStatusError = isError;
        StatusMessage = message;
    }
}
