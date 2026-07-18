using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow : Window
{
    private const int WindowWidth = 900;
    private const int WindowHeight = 620;

    public SettingsWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;

        DisplayArea displayArea = DisplayArea.GetFromWindowId(
            AppWindow.Id,
            DisplayAreaFallback.Primary);

        int centeredX = displayArea.WorkArea.X +
            (displayArea.WorkArea.Width - WindowWidth) / 2;
        int centeredY = displayArea.WorkArea.Y +
            (displayArea.WorkArea.Height - WindowHeight) / 2;

        AppWindow.MoveAndResize(new RectInt32(
            centeredX,
            centeredY,
            WindowWidth,
            WindowHeight));
    }

    public SettingsViewModel ViewModel => (SettingsViewModel)Root.DataContext;

    private async void HandleDragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args) =>
        await ViewModel.SaveOrderAsync();
}
