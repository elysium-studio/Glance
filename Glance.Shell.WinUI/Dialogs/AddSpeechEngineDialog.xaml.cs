using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class AddSpeechEngineDialog :
    ContentDialog
{
    public AddSpeechEngineDialog(AssistantModelSetupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public AssistantModelSetupViewModel ViewModel { get; }

    private async void HandlePrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue;
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        bool added = await ViewModel.AddSelectedProviderAsync();

        if (dispatcherQueue.HasThreadAccess)
        {
            args.Cancel = !added;
            deferral.Complete();
            return;
        }

        _ = dispatcherQueue.TryEnqueue(() =>
        {
            args.Cancel = !added;
            deferral.Complete();
        });
    }
}
