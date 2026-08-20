using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Inspector.WinUI;

public sealed partial class InspectorProviderSettingsViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    private readonly IDispatcher dispatcher;
    private readonly ModuleResourceTextLocalizer<InspectorModule> localizer;
    private readonly IGlanceInspectorProviderManager manager;
    private int disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusOpen))]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsStatusError { get; set; }

    public InspectorProviderSettingsViewModel(IGlanceInspectorProviderManager manager, IDispatcher dispatcher, ModuleResourceTextLocalizer<InspectorModule> localizer)
    {
        this.manager = manager;
        this.dispatcher = dispatcher;
        this.localizer = localizer;
        manager.Changed += HandleManagerChanged;
        SynchronizeProviders();
    }

    public string ModuleId => "Inspector";

    public int Order => 10;

    public ObservableCollection<InspectorProviderSettingItemViewModel> Providers { get; } = [];

    public bool CanAdd => !IsBusy;

    public bool IsStatusOpen => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool ShowEmptyState => Providers.Count == 0;

    public string RemoveDialogTitle => localizer.GetText("ProviderRemoveDialogTitle");

    public string RemoveDialogMessage => localizer.GetText("ProviderRemoveDialogMessage");

    public string RemoveDialogPrimaryButtonText => localizer.GetText("ProviderRemoveDialogPrimaryButton");

    public string RemoveDialogCloseButtonText => localizer.GetText("ProviderRemoveDialogCloseButton");

    public async Task<bool> SetEnabledAsync(InspectorProviderSettingItemViewModel provider, bool enabled)
    {
        try
        {
            await manager.SetEnabledAsync(provider.Id, enabled);
            return true;
        }
        catch
        {
            ShowStatus(localizer.GetText("ProviderChangeFailed"), true);
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
            GlanceInspectorProviderInstallResult result = await manager.InstallAsync(packagePath);

            if (!result.IsSuccessful)
            {
                ShowStatus(string.Equals(result.ErrorMessage, "IncompatibleInspectorProviderPackage", StringComparison.Ordinal) ? localizer.GetText("ProviderPackageIncompatible") : localizer.GetText("ProviderInstallFailed"), true);
                return;
            }

            ShowStatus(localizer.GetText(result.RequiresRestart ? "ProviderUpdateReady" : "ProviderInstalled"), false);
        }
        catch
        {
            ShowStatus(localizer.GetText("ProviderInstallFailed"), true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveAsync(InspectorProviderSettingItemViewModel provider)
    {
        if (!provider.CanRemove)
        {
            return;
        }

        try
        {
            if (!await manager.RemoveAsync(provider.Id))
            {
                ShowStatus(localizer.GetText("ProviderRemoveFailed"), true);
            }
        }
        catch
        {
            ShowStatus(localizer.GetText("ProviderRemoveFailed"), true);
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
            SynchronizeProviders();
        }
    });

    private void SynchronizeProviders()
    {
        IReadOnlyList<GlanceInspectorProviderExtension> extensions = manager.GetProviders();

        if (Providers.Select(provider => provider.Id).SequenceEqual(extensions.Select(extension => extension.Id), StringComparer.OrdinalIgnoreCase))
        {
            foreach (GlanceInspectorProviderExtension extension in extensions)
            {
                Providers.First(provider => string.Equals(provider.Id, extension.Id, StringComparison.OrdinalIgnoreCase)).SynchronizeEnabled(extension.IsEnabled);
            }

            return;
        }

        Providers.Clear();

        foreach (GlanceInspectorProviderExtension extension in extensions)
        {
            Providers.Add(new InspectorProviderSettingItemViewModel(extension.Id, extension.DisplayName, extension.Description, extension.IsEnabled, extension.CanRemove, SetEnabledAsync));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void ShowStatus(string message, bool isError)
    {
        IsStatusError = isError;
        StatusMessage = message;
    }
}
