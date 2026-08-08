using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SetupTourWindow :
    Window
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 660;

    private bool finished;

    public SetupTourWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;

        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width / 2) - (WindowWidth / 2);
        int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height / 2) - (WindowHeight / 2);
        AppWindow.MoveAndResize(new RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));

        ((FrameworkElement)Content).Loaded += HandleContentLoaded;
        Closed += HandleWindowClosed;
    }

    public SetupTourViewModel ViewModel => (SetupTourViewModel)((FrameworkElement)Content).DataContext;

    public Visibility ToPageVisibility(int currentPage, int page) => currentPage == page ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToNextVisibility(bool isLastPage) => isLastPage ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ToFinishVisibility(bool isLastPage) => isLastPage ? Visibility.Visible : Visibility.Collapsed;

    private void HandleContentLoaded(object sender,
        RoutedEventArgs args)
    {
        ((FrameworkElement)Content).Loaded -= HandleContentLoaded;
        ViewModel.Finished += HandleViewModelFinished;
    }

    private void HandleViewModelFinished(object? sender,
        EventArgs args)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(Finish);
            return;
        }

        Finish();
    }

    private void Finish()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        Close();
    }

    private void HandleWindowClosed(object sender,
        WindowEventArgs args)
    {
        if (!finished)
        {
            ViewModel.Cancel();
        }

        ViewModel.Finished -= HandleViewModelFinished;
        Closed -= HandleWindowClosed;
    }
}
