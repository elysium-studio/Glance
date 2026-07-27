using Glance.Shell;
using System.ComponentModel;
using Xunit;

namespace Glance.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void CurrentViewFollowsSettingsNavigation()
    {
        TestSettingViewModel overview = new();
        TestSettingViewModel detail = new();
        SettingsViewModel viewModel = new(null!, null!, null!, null!, [overview]);

        Assert.Same(overview, viewModel.CurrentView);

        viewModel.NavigateTo(detail);

        Assert.Same(detail, viewModel.CurrentView);
    }

    [Fact]
    public void NavigationCancelsActiveReordering()
    {
        TestReorderableSettingViewModel overview = new();
        TestSettingViewModel detail = new();
        SettingsViewModel viewModel = new(null!, null!, null!, null!, [overview]);

        viewModel.BeginReordering();

        Assert.True(viewModel.IsReorderingCurrentView);
        Assert.True(overview.IsReordering);

        viewModel.NavigateTo(detail);

        Assert.False(viewModel.IsReorderingCurrentView);
        Assert.False(overview.IsReordering);
        Assert.Same(detail, viewModel.CurrentView);
    }

    [Fact]
    public async Task ReorderingCanBeCompleted()
    {
        TestReorderableSettingViewModel overview = new();
        SettingsViewModel viewModel = new(null!, null!, null!, null!, [overview]);

        viewModel.BeginReordering();
        await viewModel.CompleteReorderingAsync();

        Assert.True(overview.WasCompleted);
        Assert.False(overview.IsReordering);
        Assert.False(viewModel.IsReorderingCurrentView);
    }

    private sealed class TestSettingViewModel :
        List<object>,
        ISettingViewModel
    {
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestReorderableSettingViewModel :
        List<object>,
        IReorderableSettingViewModel
    {
        public bool CanReorder => true;

        public bool IsReordering { get; private set; }

        public bool WasCompleted { get; private set; }

        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public void BeginReordering() => IsReordering = true;

        public Task CompleteReorderingAsync()
        {
            WasCompleted = true;
            IsReordering = false;
            return Task.CompletedTask;
        }

        public void CancelReordering() => IsReordering = false;

        public void Dispose()
        {
        }
    }
}
