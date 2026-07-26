using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.SpeechToText.WinUI;

public sealed partial class SpeechToTextComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceAvailabilityComponent,
    IAsyncDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly ISpeechRecognitionService recognitionService;
    private readonly ITextCopyService textCopyService;
    private readonly SpeechToTextViewModel viewModel;

    public SpeechToTextComponent(SpeechToTextViewModel viewModel,
        ISpeechRecognitionService recognitionService,
        ITextCopyService textCopyService,
        ModuleResourceTextLocalizer<SpeechToTextModule> localizer)
    {
        this.viewModel = viewModel;
        this.recognitionService = recognitionService;
        this.textCopyService = textCopyService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        SpeechToTextCompactView compactView = new(viewModel);
        SpeechToTextExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ToggleListeningRequested += HandleToggleListeningRequested;
        viewModel.EnsureModelRequested += HandleEnsureModelRequested;
        viewModel.ClearRequested += HandleClearRequested;
        viewModel.CopyRequested += HandleCopyRequested;
        recognitionService.AvailabilityChanged += HandleAvailabilityChanged;
        recognitionService.SpeechRecognized += HandleSpeechRecognized;
        recognitionService.ListeningStopped += HandleListeningStopped;

        _ = CheckAvailabilityAsync();
    }

    public string Id => "SpeechToText";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 160;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAvailable =>
        recognitionService.Availability is SpeechRecognitionAvailability.Ready or SpeechRecognitionAvailability.ModelRequired;

    public event EventHandler? AvailabilityChanged;

    public async ValueTask DisposeAsync()
    {
        viewModel.ToggleListeningRequested -= HandleToggleListeningRequested;
        viewModel.EnsureModelRequested -= HandleEnsureModelRequested;
        viewModel.ClearRequested -= HandleClearRequested;
        viewModel.CopyRequested -= HandleCopyRequested;
        recognitionService.AvailabilityChanged -= HandleAvailabilityChanged;
        recognitionService.SpeechRecognized -= HandleSpeechRecognized;
        recognitionService.ListeningStopped -= HandleListeningStopped;
        await recognitionService.DisposeAsync();
    }

    private async Task CheckAvailabilityAsync()
    {
        await recognitionService.CheckAvailabilityAsync();
        dispatcherQueue.TryEnqueue(() => viewModel.SetAvailability(recognitionService.Availability));
    }

    private async void HandleToggleListeningRequested(object? sender, EventArgs args)
    {
        viewModel.BeginPreparing();

        if (recognitionService.IsListening)
        {
            await recognitionService.StopAsync();
            dispatcherQueue.TryEnqueue(viewModel.StopListening);
            return;
        }

        bool started = await recognitionService.StartAsync(viewModel.SelectedAudioSource);
        dispatcherQueue.TryEnqueue(started ? viewModel.BeginListening : viewModel.ShowUnavailable);
    }

    private async void HandleEnsureModelRequested(object? sender, EventArgs args)
    {
        viewModel.BeginPreparing();
        bool ready = await recognitionService.EnsureModelAsync();
        dispatcherQueue.TryEnqueue(() =>
        {
            viewModel.SetAvailability(recognitionService.Availability);

            if (!ready && recognitionService.Availability == SpeechRecognitionAvailability.Unavailable)
            {
                viewModel.ShowUnavailable();
            }
        });
    }

    private void HandleClearRequested(object? sender, EventArgs args) =>
        viewModel.ClearTranscript();

    private async void HandleCopyRequested(object? sender, string text) =>
        await textCopyService.CopyAsync(text);

    private void HandleAvailabilityChanged(object? sender, SpeechRecognitionAvailabilityChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            viewModel.SetAvailability(args.Availability);
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        });

    private void HandleSpeechRecognized(object? sender, SpeechRecognizedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplyRecognition(args.Text, args.IsFinal));

    private void HandleListeningStopped(object? sender, EventArgs args) =>
        dispatcherQueue.TryEnqueue(viewModel.StopListening);
}
