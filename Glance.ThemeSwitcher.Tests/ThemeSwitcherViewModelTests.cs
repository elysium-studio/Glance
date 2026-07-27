using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.ThemeSwitcher.Tests;

public sealed class ThemeSwitcherViewModelTests
{
    [Fact]
    public async Task SelectDarkAsync_UpdatesVisibleStateAndSettings()
    {
        ThemeSwitcherSettings settings = new();
        FakeThemeController controller = new();
        ThemeSwitcherViewModel viewModel = new(controller, settings, new FakeLocalizer());
        int settingsChanges = 0;
        viewModel.SettingsChanged += (_, _) => settingsChanges++;

        await viewModel.SelectDarkAsync();

        Assert.Equal(ThemePreference.Dark, viewModel.Preference);
        Assert.Equal(ThemeVariant.Dark, viewModel.EffectiveTheme);
        Assert.Equal("Dark mode", viewModel.StatusText);
        Assert.Equal(1, settingsChanges);
    }

    [Fact]
    public async Task SelectSunsetAsync_PersistsAcquiredLocation()
    {
        ThemeSwitcherSettings settings = new();
        FakeThemeController controller = new()
        {
            SelectionResult = new ThemeChangeResult(true, ThemeVariant.Light, DateTimeOffset.UnixEpoch, 51.5, -0.1)
        };
        ThemeSwitcherViewModel viewModel = new(controller, settings, new FakeLocalizer());

        await viewModel.SelectSunsetAsync();
        ThemeSwitcherSettings written = new();
        viewModel.WriteSettings(written);

        Assert.Equal(ThemePreference.Sunset, written.Preference);
        Assert.True(written.HasLocation);
        Assert.Equal(51.5, written.Latitude);
        Assert.Equal(-0.1, written.Longitude);
    }

    [Fact]
    public async Task FailedSunsetSelection_KeepsPreviousPreference()
    {
        ThemeSwitcherSettings settings = new() { Preference = ThemePreference.Light };
        FakeThemeController controller = new()
        {
            SelectionResult = new ThemeChangeResult(false, ThemeVariant.Light, null, ErrorKey: "LocationDenied")
        };
        ThemeSwitcherViewModel viewModel = new(controller, settings, new FakeLocalizer());

        await viewModel.SelectSunsetAsync();

        Assert.Equal(ThemePreference.Light, viewModel.Preference);
        Assert.Equal("Location required", viewModel.DetailText);
    }

    [Fact]
    public async Task InitializeAsync_AdoptsTheCurrentWindowsThemeForManualMode()
    {
        ThemeSwitcherSettings settings = new() { Preference = ThemePreference.Light };
        FakeThemeController controller = new()
        {
            CurrentTheme = ThemeVariant.Dark
        };
        ThemeSwitcherViewModel viewModel = new(controller, settings, new FakeLocalizer());

        await viewModel.InitializeAsync();

        Assert.Equal(ThemePreference.Dark, viewModel.Preference);
        Assert.Equal(ThemeVariant.Dark, viewModel.EffectiveTheme);
        Assert.Equal(ThemePreference.Dark, settings.Preference);
    }

    [Fact]
    public async Task SelectDarkAsync_DispatchesStateChanges()
    {
        QueuedDispatcher dispatcher = new();
        ThemeSwitcherViewModel viewModel = new(new FakeThemeController(), new ThemeSwitcherSettings(), new FakeLocalizer(), dispatcher);

        await viewModel.SelectDarkAsync();

        Assert.True(viewModel.IsBusy);
        Assert.Single(dispatcher.Actions);

        dispatcher.Actions[0]();

        Assert.False(viewModel.IsBusy);
        Assert.Equal(ThemePreference.Dark, viewModel.Preference);
    }

    private sealed class FakeThemeController :
        IThemeController
    {
        public ThemeVariant CurrentTheme { get; init; } = ThemeVariant.Light;

        public ThemeChangeResult SelectionResult { get; init; } = new(true, ThemeVariant.Dark, null);

        public Task<ThemeChangeResult> RefreshAsync(ThemeSwitcherSettings settings,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ThemeChangeResult(true, CurrentTheme, null));

        public Task<ThemeChangeResult> SelectAsync(ThemePreference preference,
            ThemeSwitcherSettings settings,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SelectionResult);
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "LightStatus" => "Light mode",
            "DarkStatus" => "Dark mode",
            "AutomaticStatus" => "Sunset schedule",
            "ManualDetail" => "Manual",
            "UntilSunsetDetail" => $"Dark at {arguments[0]}",
            "UntilSunriseDetail" => $"Light at {arguments[0]}",
            "LocationDenied" => "Location required",
            _ => key
        };
    }

    private sealed class QueuedDispatcher :
        IDispatcher
    {
        public List<Action> Actions { get; } = [];

        public void Dispatch(Action action) =>
            Actions.Add(action);
    }
}
