using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.SpeechToText.WinUI;

public sealed partial class SpeechToTextComponent :
    IGlanceComponent,
    IGlanceActionProvider,
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

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("SpeechToText.Start",
            Id,
            "Start transcription",
            "Start transcribing microphone, system, or meeting audio.",
            [new GlanceActionParameterDescriptor("source", GlanceActionParameterType.String, "The audio source to transcribe.", false, ["microphone", "systemAudio", "meeting"])],
            Presentation: GlanceActionPresentation.Expanded),
        new GlanceActionDescriptor("SpeechToText.Stop", Id, "Stop transcription", "Stop the active transcription.")
    ];

    bool IGlanceActionProvider.IsAvailable(string actionId) => actionId switch
    {
        "SpeechToText.Start" => recognitionService.Availability == SpeechRecognitionAvailability.Ready && !recognitionService.IsListening,
        "SpeechToText.Stop" => recognitionService.IsListening,
        _ => false
    };

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "SpeechToText.Start":
                SpeechAudioSource source = request.GetString("source") switch
                {
                    "microphone" => SpeechAudioSource.Microphone,
                    "systemAudio" => SpeechAudioSource.SystemAudio,
                    _ => SpeechAudioSource.Meeting
                };
                viewModel.SelectedAudioSource = source;
                return await SetListeningAsync(true)
                    ? GlanceActionResult.Success()
                    : GlanceActionResult.Unavailable("Speech recognition could not be started.");

            case "SpeechToText.Stop":
                await SetListeningAsync(false);
                return GlanceActionResult.Success();

            default:
                return GlanceActionResult.Unavailable();
        }
    }

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
        await SetListeningAsync(!recognitionService.IsListening);
    }

    private async Task<bool> SetListeningAsync(bool listen)
    {
        viewModel.BeginPreparing();

        if (!listen)
        {
            await recognitionService.StopAsync();
            dispatcherQueue.TryEnqueue(viewModel.StopListening);
            return true;
        }

        bool started = await recognitionService.StartAsync(viewModel.SelectedAudioSource);
        dispatcherQueue.TryEnqueue(started ? viewModel.BeginListening : viewModel.ShowUnavailable);
        return started;
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
