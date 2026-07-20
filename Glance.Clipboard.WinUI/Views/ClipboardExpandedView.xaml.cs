using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardExpandedView : UserControl
{
    public ClipboardExpandedView(ClipboardShelfViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ClipboardShelfViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenEmpty(bool hasItems) =>
        hasItems ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasItems) =>
        hasItems ? Visibility.Visible : Visibility.Collapsed;

    private async void HandleCopyClick(object sender, RoutedEventArgs args)
    {
        using IDisposable operation = ClipboardDiagnostics.Begin("UI.Copy");

        try
        {
            if (GetEntry(sender) is ClipboardEntry entry)
            {
                await ViewModel.CopyAsync(entry);
            }
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("UICopyFailed", exception);
        }
    }

    private async void HandlePasteClick(object sender, RoutedEventArgs args)
    {
        using IDisposable operation = ClipboardDiagnostics.Begin("UI.Paste");

        try
        {
            if (GetEntry(sender) is ClipboardEntry entry)
            {
                await ViewModel.PasteAsync(entry);
            }
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("UIPasteFailed", exception);
        }
    }

    private async void HandleRemoveClick(object sender, RoutedEventArgs args)
    {
        using IDisposable operation = ClipboardDiagnostics.Begin("UI.Remove");

        try
        {
            if (GetEntry(sender) is ClipboardEntry entry)
            {
                await ViewModel.RemoveAsync(entry);
            }
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("UIRemoveFailed", exception);
        }
    }

    private static ClipboardEntry? GetEntry(object sender) =>
        (sender as FrameworkElement)?.DataContext as ClipboardEntry;
}
