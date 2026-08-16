using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Transcription;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Text;
using Windows.ApplicationModel;
using Windows.Media.SpeechRecognition;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace Glance.Assistant;

public sealed partial class MicrosoftOfflineAssistantProvider :
    ObservableObject,
    IGlanceAssistantProvider,
    IAsyncDisposable
{
    private const int CommandStartTimeoutMilliseconds = 10000;
    private const int CommandAudioPreRollChunks = 20;
    private const int RuntimeStartupAttempts = 3;
    private const int WakeRecognitionRestartAttempts = 3;
    private const int WakeSessionOperationTimeoutMilliseconds = 5000;
    private const int UtteranceSilenceMilliseconds = 1800;
    private readonly IGlanceAssistantCommandService commandService;
    private readonly IAudioInputSourceCatalog audioInputSources;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MicrosoftOfflineAssistantProvider> logger;
    private readonly ITranscriptionModelCatalog modelCatalog;
    private readonly ITranscriptionModelSelection modelSelection;
    private readonly ITranscriptionSessionFactory transcriptionSessions;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly StringBuilder pendingUtterance = new();
    private readonly SemaphoreSlim wakeSessionGate = new(1, 1);
    private CancellationTokenSource? commandStartCancellationTokenSource;
    private CancellationTokenSource? listeningCancellationTokenSource;
    private CancellationTokenSource? providerCancellationTokenSource;
    private CancellationTokenSource? utteranceCompletionCancellationTokenSource;
    private TaskCompletionSource<long>? commandSpeechBoundaryCompletion;
    private ITranscriptionSession? transcriptionSession;
    private RollingAudioCapture? audioCapture;
    private SpeechRecognizer? wakeRecognizer;
    private SpeechContinuousRecognitionSession? wakeRecognitionSession;
    private Task? transcriptionTask;
    private Task? wakeHealthTask;
    private bool isStarted;
    private int commandSession;
    private int healthCheckSequence;
    private int isReturningToWakeRecognition;
    private int isSwitchingToCommandRecognition;
    private int isWakeSessionActive;
    private int utteranceSession;
    private int wakeGeneration;
    private long wakeLastStateChangeTicks;
    private long wakeLastActivityTicks;
    private long wakeResultCount;
    private long commandWakeBoundary;

    [ObservableProperty]
    public partial GlanceAssistantState State { get; set; } = GlanceAssistantState.Disabled;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Voice assistant is off";

    [ObservableProperty]
    public partial string Transcript { get; set; } = "Say “Glance” or “Hey Glance”";

    public MicrosoftOfflineAssistantProvider(IGlanceAssistantCommandService commandService,
        IAssistantViewFactory viewFactory,
        IDispatcher dispatcher,
        IAudioInputSourceCatalog audioInputSources,
        ITranscriptionModelCatalog modelCatalog,
        ITranscriptionModelSelection modelSelection,
        ITranscriptionSessionFactory transcriptionSessions,
        ILogger<MicrosoftOfflineAssistantProvider> logger)
    {
        this.commandService = commandService;
        this.dispatcher = dispatcher;
        this.audioInputSources = audioInputSources;
        this.modelCatalog = modelCatalog;
        this.modelSelection = modelSelection;
        this.transcriptionSessions = transcriptionSessions;
        this.logger = logger;
        CompactIndicatorContent = viewFactory.CreateCompactIndicator(this);
        ExpandedIndicatorContent = viewFactory.CreateExpandedIndicator(this);
        OverlayContent = viewFactory.CreateOverlay(this);
        TraceWake("Provider.Created", $"Log={AssistantWakeDiagnostics.LogPath}");
        logger.LogInformation("Assistant wake diagnostics are being written to {AssistantWakeDiagnosticsPath}", AssistantWakeDiagnostics.LogPath);
    }

    public string Id => "MicrosoftOffline";

    public string DisplayName => "Microsoft offline speech";

    public object CompactIndicatorContent { get; }

    public object ExpandedIndicatorContent { get; }

    public object OverlayContent { get; }

    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        TraceWake("Provider.SetEnabled.Requested", $"Enabled={isEnabled}; {GetWakeSnapshot()}");
        await lifecycleGate.WaitAsync(cancellationToken);

        try
        {
            if (isEnabled)
            {
                if (!modelCatalog.Models.Any(model => modelCatalog.IsInstalled(model.Id)))
                {
                    throw new InvalidOperationException("Install a speech model in Glance settings before enabling the voice assistant");
                }

                if (State == GlanceAssistantState.Error)
                {
                    await StopAsync();
                }

                if (isStarted)
                {
                    if (providerCancellationTokenSource?.IsCancellationRequested == false)
                    {
                        TraceWake("Provider.SetEnabled.AlreadyRunning", GetWakeSnapshot());
                        return;
                    }

                    logger.LogWarning("The assistant runtime stopped without being disabled and will be started again");
                    await StopAsync();
                }

                isStarted = true;
                await StartAsync(cancellationToken);
            }
            else
            {
                await StopAsync();
                SetPresentation(GlanceAssistantState.Disabled, "Say “Glance” or “Hey Glance”", "Voice assistant is off");
            }
        }
        finally
        {
            _ = lifecycleGate.Release();
            TraceWake("Provider.SetEnabled.Completed", $"Enabled={isEnabled}; {GetWakeSnapshot()}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycleGate.WaitAsync();

        try
        {
            await StopAsync();
        }
        finally
        {
            _ = lifecycleGate.Release();
            lifecycleGate.Dispose();
            wakeSessionGate.Dispose();
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        TraceWake("Provider.Start.Begin", GetWakeSnapshot());

        try
        {
            AppCapabilityAccessStatus microphoneAccess = await AppCapability.Create("microphone").RequestAccessAsync();
            TraceWake("Provider.MicrophoneAccess", $"Status={microphoneAccess}");

            if (microphoneAccess != AppCapabilityAccessStatus.Allowed)
            {
                throw new UnauthorizedAccessException($"Microphone access is {microphoneAccess}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            providerCancellationTokenSource = new CancellationTokenSource();
            await StartAudioCaptureWithRetryAsync(providerCancellationTokenSource.Token);
            await StartWakeRecognitionWithRetryAsync();
            wakeHealthTask = MonitorWakeRecognitionAsync(providerCancellationTokenSource.Token);
            TraceWake("Provider.Start.Ready", GetWakeSnapshot());

            SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", "Listening");
        }
        catch (Exception exception)
        {
            TraceWake("Provider.Start.Failed", $"{exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogError(exception, "Failed to start the Microsoft offline assistant provider");
            await StopAsync();
            SetPresentation(GlanceAssistantState.Error, "Voice assistant unavailable", exception.Message);
        }
    }

    private async Task StopAsync()
    {
        TraceWake("Provider.Stop.Begin", GetWakeSnapshot());
        isStarted = false;
        CancelPendingUtterance();

        if (providerCancellationTokenSource is not null)
        {
            await providerCancellationTokenSource.CancelAsync();
        }

        await StopCommandRecognitionAsync();
        await RunOnDispatcherAsync(StopWakeRecognitionAsync);

        if (wakeHealthTask is not null)
        {
            try
            {
                await wakeHealthTask;
            }
            catch (OperationCanceledException)
            {
            }

            wakeHealthTask = null;
        }

        if (audioCapture is not null)
        {
            await audioCapture.DisposeAsync();
            audioCapture = null;
        }

        providerCancellationTokenSource?.Dispose();
        providerCancellationTokenSource = null;
        TraceWake("Provider.Stop.Completed", GetWakeSnapshot());
    }

    private async Task StartWakeRecognitionAsync()
    {
        if (wakeRecognizer is not null)
        {
            TraceWake("Wake.Start.Skipped", $"Reason=RecognizerAlreadyExists; {GetWakeSnapshot()}");
            return;
        }

        int generation = Interlocked.Increment(ref wakeGeneration);
        TraceWake("Wake.Start.Begin", $"Generation={generation}; Language={SpeechRecognizer.SystemSpeechLanguage?.LanguageTag}; {GetWakeSnapshot()}");
        SpeechRecognizer recognizer = new(SpeechRecognizer.SystemSpeechLanguage);
        recognizer.Constraints.Add(new SpeechRecognitionListConstraint((string[])["Glance", "Hey Glance"], "GlanceWakePhrase"));
        recognizer.StateChanged += HandleWakeRecognizerStateChanged;
        recognizer.RecognitionQualityDegrading += HandleWakeRecognitionQualityDegrading;
        SpeechRecognitionCompilationResult compilation = await recognizer.CompileConstraintsAsync();
        TraceWake("Wake.ConstraintsCompiled", $"Generation={generation}; Status={compilation.Status}");

        if (compilation.Status != SpeechRecognitionResultStatus.Success)
        {
            recognizer.StateChanged -= HandleWakeRecognizerStateChanged;
            recognizer.RecognitionQualityDegrading -= HandleWakeRecognitionQualityDegrading;
            recognizer.Dispose();
            throw new InvalidOperationException($"Windows could not compile the wake phrase: {compilation.Status}");
        }

        SpeechContinuousRecognitionSession session = recognizer.ContinuousRecognitionSession;
        session.ResultGenerated += HandleWakeResultGenerated;
        session.Completed += HandleWakeRecognitionCompleted;
        wakeRecognizer = recognizer;
        wakeRecognitionSession = session;
        try
        {
            await WaitForWakeOperationAsync(session.StartAsync(), "start");
            _ = Interlocked.Exchange(ref isWakeSessionActive, 1);
            TraceWake("Wake.Start.Completed", $"Generation={generation}; RecognizerState={recognizer.State}; {GetWakeSnapshot()}");
        }
        catch (Exception exception)
        {
            TraceWake("Wake.Start.Failed", $"Generation={generation}; {exception.GetType().Name}: {exception.Message}");
            wakeRecognizer = null;
            wakeRecognitionSession = null;
            _ = Interlocked.Exchange(ref isWakeSessionActive, 0);
            session.ResultGenerated -= HandleWakeResultGenerated;
            session.Completed -= HandleWakeRecognitionCompleted;
            recognizer.StateChanged -= HandleWakeRecognizerStateChanged;
            recognizer.RecognitionQualityDegrading -= HandleWakeRecognitionQualityDegrading;
            recognizer.Dispose();
            throw;
        }

        SetPresentation(GlanceAssistantState.ListeningForWakeWord,
            "Say “Glance” or “Hey Glance”",
            "Listening");
    }

    private async Task StopWakeRecognitionAsync()
    {
        SpeechRecognizer? recognizer = wakeRecognizer;
        SpeechContinuousRecognitionSession? session = wakeRecognitionSession;
        bool wasActive = Interlocked.Exchange(ref isWakeSessionActive, 0) != 0;
        int generation = Volatile.Read(ref wakeGeneration);
        TraceWake("Wake.Stop.Begin", $"Generation={generation}; {GetWakeSnapshot()}");
        wakeRecognizer = null;
        wakeRecognitionSession = null;

        if (recognizer is null || session is null)
        {
            TraceWake("Wake.Stop.Skipped", $"Generation={generation}; Reason=NoActiveSession");
            return;
        }

        session.ResultGenerated -= HandleWakeResultGenerated;
        session.Completed -= HandleWakeRecognitionCompleted;
        recognizer.StateChanged -= HandleWakeRecognizerStateChanged;
        recognizer.RecognitionQualityDegrading -= HandleWakeRecognitionQualityDegrading;

        try
        {
            if (wasActive)
            {
                await WaitForWakeOperationAsync(session.CancelAsync(), "cancel");
                TraceWake("Wake.Stop.Cancelled", $"Generation={generation}; RecognizerState={recognizer.State}");
            }
        }
        catch (Exception exception)
        {
            TraceWake("Wake.Stop.CancelFailed", $"Generation={generation}; {exception.GetType().Name}: {exception.Message}");
        }

        recognizer.Dispose();
        TraceWake("Wake.Stop.Completed", $"Generation={generation}");
    }

    private void HandleWakeRecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
    {
        long timestamp = DateTime.UtcNow.Ticks;
        _ = Interlocked.Exchange(ref wakeLastStateChangeTicks, timestamp);
        _ = Interlocked.Exchange(ref wakeLastActivityTicks, timestamp);
        string ownership = ReferenceEquals(sender, wakeRecognizer) ? "Current" : "Stale";

        if (ownership == "Current" &&
            State == GlanceAssistantState.ListeningForCommand &&
            args.State == SpeechRecognizerState.SoundStarted)
        {
            long speechBoundary = audioCapture?.CreateCheckpoint() ?? 0;
            long wakeBoundary = Interlocked.Read(ref commandWakeBoundary);
            long replayBoundary = Math.Max(wakeBoundary, speechBoundary - CommandAudioPreRollChunks);

            if (commandSpeechBoundaryCompletion?.TrySetResult(replayBoundary) == true)
            {
                TraceWake("Command.SpeechStarted", $"SpeechBoundary={speechBoundary}; ReplayBoundary={replayBoundary}; WakeBoundary={wakeBoundary}; {GetWakeSnapshot()}");
            }
        }
        TraceWake("Wake.StateChanged", $"Generation={Volatile.Read(ref wakeGeneration)}; Ownership={ownership}; State={args.State}; ProviderState={State}");
    }

    private void HandleWakeRecognitionQualityDegrading(SpeechRecognizer sender, SpeechRecognitionQualityDegradingEventArgs args)
    {
        string ownership = ReferenceEquals(sender, wakeRecognizer) ? "Current" : "Stale";
        TraceWake("Wake.QualityDegrading", $"Generation={Volatile.Read(ref wakeGeneration)}; Ownership={ownership}; Problem={args.Problem}; ProviderState={State}");
    }

    private void HandleWakeResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (!ReferenceEquals(sender, wakeRecognitionSession))
        {
            TraceWake("Wake.ResultIgnored", $"Reason=StaleSession; Text={args.Result.Text}; Confidence={args.Result.Confidence}; {GetWakeSnapshot()}");
            return;
        }

        if (State != GlanceAssistantState.ListeningForWakeWord)
        {
            TraceWake("Wake.ResultIgnored", $"Reason=ProviderState; Text={args.Result.Text}; Confidence={args.Result.Confidence}; {GetWakeSnapshot()}");
            return;
        }

        long resultCount = Interlocked.Increment(ref wakeResultCount);
        _ = Interlocked.Exchange(ref wakeLastActivityTicks, DateTime.UtcNow.Ticks);
        TraceWake("Wake.Result", $"Count={resultCount}; Text={args.Result.Text}; Confidence={args.Result.Confidence}; RawConfidence={args.Result.RawConfidence:F4}; {GetWakeSnapshot()}");
        logger.LogInformation("Wake recognition heard {WakeText} with {WakeConfidence} confidence", args.Result.Text, args.Result.Confidence);

        if (args.Result.Confidence == SpeechRecognitionConfidence.Rejected)
        {
            TraceWake("Wake.ResultRejected", $"Count={resultCount}; Text={args.Result.Text}");
            int rejectedGeneration = Volatile.Read(ref wakeGeneration);
            Dispatch(() => _ = RecoverRejectedWakeRecognitionAsync(rejectedGeneration));
            return;
        }

        if (Interlocked.CompareExchange(ref isSwitchingToCommandRecognition, 1, 0) != 0)
        {
            TraceWake("Wake.ResultIgnored", $"Reason=CommandSwitchAlreadyActive; Count={resultCount}; Text={args.Result.Text}; {GetWakeSnapshot()}");
            return;
        }

        string wakeText = args.Result.Text;
        long wakeBoundary = audioCapture?.CreateCheckpoint() ?? 0;
        Dispatch(() => _ = BeginCommandRecognitionAsync(wakeText, wakeBoundary));
    }

    private async Task BeginCommandRecognitionAsync(string wakeText, long wakeBoundary)
    {
        try
        {
            if (State != GlanceAssistantState.ListeningForWakeWord || providerCancellationTokenSource?.IsCancellationRequested != false)
            {
                return;
            }

            TraceWake("Wake.ConstrainedDetected", $"Text={wakeText}; {GetWakeSnapshot()}");
            _ = Interlocked.Exchange(ref commandWakeBoundary, wakeBoundary);
            BeginCommandWindow();
            await StartCommandRecognitionAsync(providerCancellationTokenSource.Token);
            TraceWake("Command.Start.Ready", GetWakeSnapshot());
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            TraceWake("Command.Start.Failed", $"{exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogError(exception, "Failed to start command transcription after the wake phrase");
            await ReturnToWakeRecognitionAsync("Voice command unavailable");
        }
        finally
        {
            _ = Interlocked.Exchange(ref isSwitchingToCommandRecognition, 0);
        }
    }

    private void HandleWakeRecognitionCompleted(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        if (!ReferenceEquals(sender, wakeRecognitionSession))
        {
            TraceWake("Wake.CompletedIgnored", $"Reason=StaleSession; Status={args.Status}; {GetWakeSnapshot()}");
            return;
        }

        if (Volatile.Read(ref isReturningToWakeRecognition) != 0)
        {
            TraceWake("Wake.CompletedIgnored", $"Reason=SessionRefresh; Status={args.Status}; {GetWakeSnapshot()}");
            return;
        }

        TraceWake("Wake.CompletedUnexpectedly", $"Status={args.Status}; {GetWakeSnapshot()}");
        logger.LogWarning("Wake recognition completed unexpectedly with {WakeStatus}", args.Status);
        Dispatch(() => _ = RecoverWakeRecognitionAsync());
    }

    private async Task RecoverWakeRecognitionAsync()
    {
        if (providerCancellationTokenSource?.IsCancellationRequested != false)
        {
            TraceWake("Wake.Recovery.Skipped", $"Reason=ProviderCancelled; {GetWakeSnapshot()}");
            return;
        }

        if (Volatile.Read(ref isSwitchingToCommandRecognition) != 0)
        {
            TraceWake("Wake.Recovery.Skipped", $"Reason=CommandSwitchActive; {GetWakeSnapshot()}");
            return;
        }

        if (Volatile.Read(ref isReturningToWakeRecognition) != 0)
        {
            TraceWake("Wake.Recovery.Skipped", $"Reason=SessionRefresh; {GetWakeSnapshot()}");
            return;
        }

        if (Interlocked.CompareExchange(ref isReturningToWakeRecognition, 1, 0) != 0)
        {
            TraceWake("Wake.Recovery.Skipped", $"Reason=RecoveryAlreadyActive; {GetWakeSnapshot()}");
            return;
        }

        TraceWake("Wake.Recovery.Begin", GetWakeSnapshot());

        try
        {
            await Task.Delay(250, providerCancellationTokenSource.Token);
            await StartWakeRecognitionWithRetryAsync();
            TraceWake("Wake.Recovery.Completed", GetWakeSnapshot());
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            TraceWake("Wake.Recovery.Failed", $"{exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogError(exception, "Failed to restart wake recognition");
            SetPresentation(GlanceAssistantState.Preparing, "Voice assistant is recovering", "Restarting wake recognition");
        }
        finally
        {
            _ = Interlocked.Exchange(ref isReturningToWakeRecognition, 0);
            TraceWake("Wake.Recovery.Ended", GetWakeSnapshot());
        }
    }

    private async Task RecoverRejectedWakeRecognitionAsync(int generation)
    {
        try
        {
            await Task.Delay(500, providerCancellationTokenSource?.Token ?? CancellationToken.None);
            bool isCapturing = false;
            await RunOnDispatcherAsync(() =>
            {
                isCapturing = wakeRecognizer?.State == SpeechRecognizerState.Capturing;
                return Task.CompletedTask;
            });

            if (generation != Volatile.Read(ref wakeGeneration) ||
                State != GlanceAssistantState.ListeningForWakeWord ||
                isCapturing)
            {
                return;
            }

            TraceWake("Wake.Rejected.RefreshRequired", $"Generation={generation}; {GetWakeSnapshot()}");
            await RecoverWakeRecognitionAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task MonitorWakeRecognitionAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                int healthSequence = Interlocked.Increment(ref healthCheckSequence);

                if (State is not (GlanceAssistantState.Preparing or GlanceAssistantState.ListeningForWakeWord) ||
                    Volatile.Read(ref isSwitchingToCommandRecognition) != 0 ||
                    Volatile.Read(ref isReturningToWakeRecognition) != 0)
                {
                    if (healthSequence % 5 == 0)
                    {
                        TraceWake("Wake.Health.Skipped", $"Sequence={healthSequence}; {GetWakeSnapshot()}");
                    }

                    continue;
                }

                try
                {
                    if (audioCapture?.IsHealthy != true)
                    {
                        logger.LogWarning(audioCapture?.Failure, "Assistant audio capture stopped unexpectedly and will be restarted");
                        await RestartAudioCaptureAsync(cancellationToken);
                    }

                    bool restartRequired = false;
                    bool refreshRequired = false;
                    SpeechRecognizerState? recognizerState = null;
                    TimeSpan stateAge = TimeSpan.Zero;
                    TimeSpan microphoneActivityAge = TimeSpan.MaxValue;
                    await RunOnDispatcherAsync(() =>
                    {
                        recognizerState = wakeRecognizer?.State;
                        long lastStateChangeTicks = Interlocked.Read(ref wakeLastStateChangeTicks);
                        long lastWakeActivityTicks = Interlocked.Read(ref wakeLastActivityTicks);
                        long lastMicrophoneActivityTicks = audioCapture?.LastSpeechLikeAudioTicks ?? 0;
                        stateAge = lastStateChangeTicks == 0 ? TimeSpan.Zero : DateTime.UtcNow - new DateTime(lastStateChangeTicks, DateTimeKind.Utc);
                        microphoneActivityAge = lastMicrophoneActivityTicks == 0 ? TimeSpan.MaxValue : DateTime.UtcNow - new DateTime(lastMicrophoneActivityTicks, DateTimeKind.Utc);
                        restartRequired = Volatile.Read(ref isWakeSessionActive) == 0 ||
                            wakeRecognizer is null ||
                            wakeRecognitionSession is null ||
                            recognizerState is SpeechRecognizerState.Idle or SpeechRecognizerState.Paused or SpeechRecognizerState.Processing;
                        refreshRequired = !restartRequired &&
                            ((recognizerState is SpeechRecognizerState.SoundStarted or SpeechRecognizerState.SoundEnded or SpeechRecognizerState.SpeechDetected &&
                                stateAge >= TimeSpan.FromSeconds(10)) ||
                            (recognizerState == SpeechRecognizerState.Capturing &&
                                lastMicrophoneActivityTicks > lastWakeActivityTicks &&
                                microphoneActivityAge <= TimeSpan.FromSeconds(4) &&
                                stateAge >= TimeSpan.FromSeconds(4)));
                        return Task.CompletedTask;
                    });

                    if (healthSequence % 5 == 0)
                    {
                        TraceWake("Wake.Health", $"Sequence={healthSequence}; RecognizerState={recognizerState}; StateAge={stateAge.TotalSeconds:F1}s; MicrophoneActivityAge={microphoneActivityAge.TotalSeconds:F1}s; RestartRequired={restartRequired}; RefreshRequired={refreshRequired}; {GetWakeSnapshot()}");
                    }

                    if (restartRequired)
                    {
                        TraceWake("Wake.Health.RestartRequired", $"Sequence={healthSequence}; RecognizerState={recognizerState}; {GetWakeSnapshot()}");
                        logger.LogWarning("Wake recognition became inactive in state {WakeRecognizerState} and will be restarted", recognizerState);
                        await RecoverWakeRecognitionAsync();
                    }
                    else if (refreshRequired)
                    {
                        TraceWake("Wake.Health.RefreshRequired", $"Sequence={healthSequence}; RecognizerState={recognizerState}; StateAge={stateAge.TotalSeconds:F1}s; MicrophoneActivityAge={microphoneActivityAge.TotalSeconds:F1}s; {GetWakeSnapshot()}");
                        logger.LogWarning("Wake recognition remained in state {WakeRecognizerState} for {WakeRecognizerStateAge} and its session will be refreshed", recognizerState, stateAge);
                        await RecoverWakeRecognitionAsync();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    TraceWake("Wake.Health.Failed", $"Sequence={healthSequence}; {exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
                    logger.LogWarning(exception, "The assistant health check failed and will retry");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RestartAudioCaptureAsync(CancellationToken cancellationToken)
    {
        TraceWake("Audio.Restart.Begin", audioCapture?.GetDiagnosticState() ?? "Capture=None");
        RollingAudioCapture? previousCapture = audioCapture;
        audioCapture = null;

        if (previousCapture is not null)
        {
            await previousCapture.DisposeAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
        RollingAudioCapture nextCapture = new(cancellationToken);
        nextCapture.Start();
        audioCapture = nextCapture;
        TraceWake("Audio.Restart.Completed", nextCapture.GetDiagnosticState());
    }

    private async Task StartAudioCaptureWithRetryAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;

        for (int attempt = 1; attempt <= RuntimeStartupAttempts; attempt++)
        {
            try
            {
                await RestartAudioCaptureAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failure = exception;
                logger.LogWarning(exception, "Assistant audio capture startup attempt {AudioCaptureAttempt} failed", attempt);

                if (attempt < RuntimeStartupAttempts)
                {
                    await Task.Delay(attempt * 250, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException("Windows audio capture could not be started", failure);
    }

    private async Task StartCommandRecognitionAsync(CancellationToken cancellationToken)
    {
        if (transcriptionSession is not null)
        {
            return;
        }

        listeningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken listeningCancellationToken = listeningCancellationTokenSource.Token;
        AudioInputSource source = (await audioInputSources.GetSourcesAsync(listeningCancellationToken))
            .FirstOrDefault(item => item.IsDefault) ?? throw new InvalidOperationException("No microphone is available");
        string modelId = TranscriptionModelResolver.ResolveInstalledModel(modelCatalog, modelSelection) ??
            throw new InvalidOperationException("Install a speech model in Glance settings before using voice commands");
        transcriptionSession = await transcriptionSessions.CreateAsync(new TranscriptionSessionOptions(modelId,
            source.Id,
            "en"),
            listeningCancellationToken);
        transcriptionTask = ReadTranscriptionAsync(transcriptionSession, listeningCancellationToken);
        TraceWake("Command.Start.Completed", GetWakeSnapshot());
    }

    private async Task ReadTranscriptionAsync(ITranscriptionSession session, CancellationToken cancellationToken)
    {
        bool recoveryRequired = false;

        try
        {
            await foreach (TranscriptionResult result in session.GetResultsAsync(cancellationToken))
            {
                string text = result.Text.Trim();

                if (result.IsFinal && !string.IsNullOrWhiteSpace(text))
                {
                    TraceWake("Command.Transcription.Result", $"Text={text}; {GetWakeSnapshot()}");
                    Dispatch(() => ProcessRecognizedText(text));
                }
            }

            TraceWake("Command.Transcription.StreamEnded", GetWakeSnapshot());
            recoveryRequired = !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TraceWake("Command.Transcription.Cancelled", GetWakeSnapshot());
        }
        catch (Exception exception)
        {
            TraceWake("Command.Transcription.Failed", $"{exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogWarning(exception, "The continuous assistant transcription stream stopped unexpectedly");
            recoveryRequired = true;
        }
        finally
        {
            if (recoveryRequired && ReferenceEquals(session, transcriptionSession))
            {
                transcriptionTask = null;
                Dispatch(() => _ = ReturnToWakeRecognitionAsync("Voice command stopped"));
            }
        }
    }

    private void ProcessRecognizedText(string text)
    {
        if (State == GlanceAssistantState.ListeningForWakeWord)
        {
            TraceWake("Transcription.Ignored", $"Reason=ConstrainedWakeRecognitionActive; Text={text}; {GetWakeSnapshot()}");
            return;
        }

        if (State == GlanceAssistantState.ListeningForCommand)
        {
            AppendCommandText(text);
            return;
        }

        TraceWake("Transcription.Ignored", $"Reason=ProviderState; Text={text}; {GetWakeSnapshot()}");
    }

    private void AppendCommandText(string text)
    {
        if (State != GlanceAssistantState.ListeningForCommand)
        {
            TraceWake("Command.Transcription.Ignored", $"Reason=ProviderState; Text={text}; {GetWakeSnapshot()}");
            return;
        }

        if (pendingUtterance.Length == 0)
        {
            text = text.TrimStart(' ', ',', '.', '!', '?', ':', ';', '-');

            if (string.IsNullOrWhiteSpace(text))
            {
                TraceWake("Command.Transcription.Ignored", $"Reason=LeadingPunctuation; {GetWakeSnapshot()}");
                return;
            }
        }

        commandStartCancellationTokenSource?.Cancel();

        if (pendingUtterance.Length > 0 && !char.IsWhiteSpace(pendingUtterance[^1]) && !char.IsPunctuation(text[0]))
        {
            _ = pendingUtterance.Append(' ');
        }

        _ = pendingUtterance.Append(text);
        Transcript = pendingUtterance.ToString();
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
        int session = ++utteranceSession;
        TraceWake("Command.Utterance.Extended", $"Session={session}; Text={pendingUtterance}; {GetWakeSnapshot()}");
        _ = CompleteUtteranceAfterSilenceAsync(session, utteranceCompletionCancellationTokenSource.Token);
    }

    private async Task CompleteUtteranceAfterSilenceAsync(int session, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(UtteranceSilenceMilliseconds, cancellationToken);
            Dispatch(() => CompleteUtterance(session));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CompleteUtterance(int session)
    {
        if (session != utteranceSession || pendingUtterance.Length == 0)
        {
            return;
        }

        string command = pendingUtterance.ToString().Trim();
        TraceWake("Command.Utterance.Completed", $"Session={session}; Command={command}; {GetWakeSnapshot()}");
        _ = pendingUtterance.Clear();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        _ = CompleteCommandAsync(command);
    }

    private void BeginCommandWindow(string transcript = "What can I help with?",
        string status = "Listening for your command")
    {
        _ = (commandSpeechBoundaryCompletion?.TrySetCanceled());
        commandSpeechBoundaryCompletion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = pendingUtterance.Clear();
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        utteranceSession++;
        commandStartCancellationTokenSource?.Cancel();
        commandStartCancellationTokenSource?.Dispose();
        commandStartCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
        int session = ++commandSession;
        TraceWake("Command.Window.Started", $"Session={session}; Transcript={transcript}; Status={status}; {GetWakeSnapshot()}");
        SetPresentation(GlanceAssistantState.ListeningForCommand, transcript, status);
        _ = CancelCommandWindowAfterTimeoutAsync(session, commandStartCancellationTokenSource.Token);
    }

    private async Task CancelCommandWindowAfterTimeoutAsync(int session, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CommandStartTimeoutMilliseconds, cancellationToken);
            Dispatch(() =>
            {
                if (session == commandSession && State == GlanceAssistantState.ListeningForCommand)
                {
                    TraceWake("Command.Window.TimedOut", $"Session={session}; {GetWakeSnapshot()}");
                    _ = ReturnToWakeRecognitionAsync("No command heard");
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CompleteCommandAsync(string command)
    {
        TraceWake("Command.Execute.Begin", $"Command={command}; {GetWakeSnapshot()}");

        try
        {
            CancellationToken cancellationToken = providerCancellationTokenSource?.Token ?? CancellationToken.None;
            commandStartCancellationTokenSource?.Cancel();
            SetPresentation(GlanceAssistantState.ProcessingCommand, command, "Working on it");
            GlanceAssistantCommandResult result = await commandService.ExecuteAsync(command, cancellationToken);

            if (!result.Handled)
            {
                TraceWake("Command.Execute.NotHandled", $"Command={command}; {GetWakeSnapshot()}");
                await PromptForAnotherCommandAsync(cancellationToken, result.Response, result.Guidance);
                return;
            }

            SetPresentation(GlanceAssistantState.ProcessingCommand,
                string.IsNullOrWhiteSpace(result.Response) ? command : result.Response,
                "Done");
            await Task.Delay(700, cancellationToken);
            TraceWake("Command.Execute.Handled", $"Command={command}; Response={result.Response}; {GetWakeSnapshot()}");
            await ReturnToWakeRecognitionAsync("Listening");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            TraceWake("Command.Execute.Failed", $"Command={command}; {exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogError(exception, "Failed to execute assistant command {AssistantCommand}", command);

            if (providerCancellationTokenSource?.IsCancellationRequested == false)
            {
                await PromptForAnotherCommandAsync(providerCancellationTokenSource.Token);
            }
        }
    }

    private async Task PromptForAnotherCommandAsync(CancellationToken cancellationToken,
        string? reason = null,
        string? guidance = null)
    {
        if (transcriptionSession is not null && listeningCancellationTokenSource?.IsCancellationRequested == false)
        {
            BeginCommandWindow(string.IsNullOrWhiteSpace(reason) ? "I didn't understand that, try again" : reason,
                string.IsNullOrWhiteSpace(guidance) ? "Listening for command" : guidance);
            return;
        }

        await ReturnToWakeRecognitionAsync("Listening");
    }

    private async Task ReturnToWakeRecognitionAsync(string status)
    {
        if (Interlocked.CompareExchange(ref isReturningToWakeRecognition, 1, 0) != 0)
        {
            TraceWake("Wake.Return.Skipped", $"Reason=ReturnAlreadyActive; Status={status}; {GetWakeSnapshot()}");
            return;
        }

        TraceWake("Wake.Return.Begin", $"Status={status}; {GetWakeSnapshot()}");

        try
        {
            if (providerCancellationTokenSource?.IsCancellationRequested != false)
            {
                return;
            }

            CancelPendingUtterance();
            await StopCommandRecognitionAsync();
            TraceWake("Wake.Return.Ready", $"Status={status}; {GetWakeSnapshot()}");
            SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", status);
        }
        catch (OperationCanceledException) when (providerCancellationTokenSource?.IsCancellationRequested != false)
        {
        }
        catch (Exception exception)
        {
            TraceWake("Wake.Return.Failed", $"Status={status}; {exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
            logger.LogError(exception, "Failed to return to wake recognition");
            SetPresentation(GlanceAssistantState.Preparing, "Voice assistant is recovering", "Restarting wake recognition");
        }
        finally
        {
            _ = Interlocked.Exchange(ref isReturningToWakeRecognition, 0);
            TraceWake("Wake.Return.Ended", $"Status={status}; {GetWakeSnapshot()}");
        }
    }

    private async Task StartWakeRecognitionWithRetryAsync()
    {
        await wakeSessionGate.WaitAsync(providerCancellationTokenSource?.Token ?? CancellationToken.None);

        try
        {
            await StartWakeRecognitionWithRetryCoreAsync();
        }
        finally
        {
            _ = wakeSessionGate.Release();
        }
    }

    private async Task StartWakeRecognitionWithRetryCoreAsync()
    {
        Exception? failure = null;

        for (int attempt = 1; attempt <= WakeRecognitionRestartAttempts; attempt++)
        {
            TraceWake("Wake.Restart.Attempt", $"Attempt={attempt}; {GetWakeSnapshot()}");

            try
            {
                await RunOnDispatcherAsync(async () =>
                {
                    await StopWakeRecognitionAsync();
                    await StartWakeRecognitionAsync();
                });
                TraceWake("Wake.Restart.Succeeded", $"Attempt={attempt}; {GetWakeSnapshot()}");
                return;
            }
            catch (OperationCanceledException) when (providerCancellationTokenSource?.IsCancellationRequested != false)
            {
                throw;
            }
            catch (Exception exception)
            {
                failure = exception;
                TraceWake("Wake.Restart.Failed", $"Attempt={attempt}; {exception.GetType().Name}: {exception.Message}; {GetWakeSnapshot()}");
                logger.LogWarning(exception, "Wake recognition restart attempt {WakeRecognitionAttempt} failed", attempt);

                if (attempt < WakeRecognitionRestartAttempts)
                {
                    await Task.Delay(attempt * 250, providerCancellationTokenSource?.Token ?? CancellationToken.None);
                }
            }
        }

        throw new InvalidOperationException("Windows wake recognition could not be restarted", failure);
    }

    private async Task StopCommandRecognitionAsync()
    {
        TraceWake("Command.Stop.Begin", GetWakeSnapshot());
        CancelPendingUtterance();
        CancellationTokenSource? cancellationTokenSource = listeningCancellationTokenSource;
        listeningCancellationTokenSource = null;
        ITranscriptionSession? session = transcriptionSession;
        transcriptionSession = null;
        Task? readingTask = transcriptionTask;
        transcriptionTask = null;

        if (cancellationTokenSource is not null)
        {
            await cancellationTokenSource.CancelAsync();
        }

        if (session is not null)
        {
            try
            {
                using CancellationTokenSource stopCancellationTokenSource = new(TimeSpan.FromSeconds(2));
                await session.StopAsync(stopCancellationTokenSource.Token);
                await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
        }

        if (readingTask is not null)
        {
            try
            {
                await readingTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
        }

        cancellationTokenSource?.Dispose();
        TraceWake("Command.Stop.Completed", GetWakeSnapshot());
    }

    private static async Task WaitForWakeOperationAsync(Windows.Foundation.IAsyncAction operation, string operationName)
    {
        try
        {
            await operation.AsTask().WaitAsync(TimeSpan.FromMilliseconds(WakeSessionOperationTimeoutMilliseconds));
        }
        catch (TimeoutException exception)
        {
            operation.Cancel();
            throw new TimeoutException($"Windows wake recognition did not complete its {operationName} operation", exception);
        }
    }

    private void CancelPendingUtterance()
    {
        commandSession++;
        commandStartCancellationTokenSource?.Cancel();
        commandStartCancellationTokenSource?.Dispose();
        commandStartCancellationTokenSource = null;
        _ = (commandSpeechBoundaryCompletion?.TrySetCanceled());
        commandSpeechBoundaryCompletion = null;
        _ = Interlocked.Exchange(ref commandWakeBoundary, 0);
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        utteranceSession++;
        _ = pendingUtterance.Clear();
    }

    private void SetPresentation(GlanceAssistantState state, string transcript, string status)
    {
        TraceWake("Provider.Presentation", $"NextState={state}; Transcript={transcript}; Status={status}; CurrentState={State}");
        dispatcher.Dispatch(() =>
        {
            State = state;
            Transcript = transcript;
            StatusText = status;
        });
    }

    private void Dispatch(Action action) => dispatcher.Dispatch(action);

    private string GetWakeSnapshot() => $"ProviderState={State}; Started={isStarted}; ProviderCancelled={providerCancellationTokenSource?.IsCancellationRequested}; " +
        $"WakeGeneration={Volatile.Read(ref wakeGeneration)}; WakeResults={Interlocked.Read(ref wakeResultCount)}; " +
        $"RecognizerPresent={wakeRecognizer is not null}; SessionPresent={wakeRecognitionSession is not null}; " +
        $"SessionActive={Volatile.Read(ref isWakeSessionActive)}; " +
        $"Switching={Volatile.Read(ref isSwitchingToCommandRecognition)}; Returning={Volatile.Read(ref isReturningToWakeRecognition)}; " +
        $"CommandSession={commandSession}; CommandBoundaryReady={commandSpeechBoundaryCompletion?.Task.IsCompletedSuccessfully}; TranscriptionPresent={transcriptionSession is not null}; " +
        $"ListeningCancelled={listeningCancellationTokenSource?.IsCancellationRequested}; " +
        $"Audio=[{audioCapture?.GetDiagnosticState() ?? "None"}]";

    private static void TraceWake(string eventName, string details) => AssistantWakeDiagnostics.Write(eventName, details);

    private Task RunOnDispatcherAsync(Func<Task> action)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Dispatch(async () =>
        {
            try
            {
                await action();
                _ = completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Package.Current.Id.Name);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class RollingAudioCapture(CancellationToken cancellationToken) :
        IAsyncDisposable
    {
        private const int BufferedChunkCount = 75;
        private readonly Queue<BufferedAudioChunk> bufferedAudio = new();
        private readonly CancellationTokenSource captureCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        private readonly object gate = new();
        private long audioSequence;
        private long dataBytes;
        private long dataCallbackCount;
        private long lastDataTimestamp;
        private long lastSpeechLikeAudioTimestamp;
        private WasapiCapture? capture;
        private Task? capturePumpTask;
        private Exception? recordingFailure;
        private int isRecording;
        private BufferedWaveProvider? sourceBuffer;

        public Exception? Failure => recordingFailure ?? capturePumpTask?.Exception?.GetBaseException();

        public bool IsHealthy => Volatile.Read(ref isRecording) != 0 && capturePumpTask?.IsCompleted == false;

        public long LastSpeechLikeAudioTicks => Interlocked.Read(ref lastSpeechLikeAudioTimestamp);

        public long CreateCheckpoint()
        {
            lock (gate)
            {
                return audioSequence;
            }
        }

        public string GetDiagnosticState()
        {
            lock (gate)
            {
                DateTimeOffset? lastData = lastDataTimestamp == 0 ? null : new DateTimeOffset(lastDataTimestamp, TimeSpan.Zero);
                return $"Healthy={IsHealthy}; Recording={Volatile.Read(ref isRecording)}; Sequence={audioSequence}; Buffered={bufferedAudio.Count}; " +
                    $"Callbacks={Interlocked.Read(ref dataCallbackCount)}; Bytes={Interlocked.Read(ref dataBytes)}; LastDataUtc={lastData:O}; " +
                    $"PumpStatus={capturePumpTask?.Status}; Failure={Failure?.Message}";
            }
        }

        public void Start()
        {
            capture = new WasapiCapture();
            AssistantWakeDiagnostics.Write("Audio.Start.Begin", $"WaveFormat={capture.WaveFormat}; Device={capture.GetType().Name}");
            sourceBuffer = new BufferedWaveProvider(capture.WaveFormat) { DiscardOnBufferOverflow = true, ReadFully = true };
            ISampleProvider sampleProvider = sourceBuffer.ToSampleProvider();
            sampleProvider = sampleProvider.WaveFormat.Channels == 1 ? sampleProvider : new DownmixSampleProvider(sampleProvider);
            sampleProvider = sampleProvider.WaveFormat.SampleRate == 16000 ? sampleProvider : new WdlResamplingSampleProvider(sampleProvider, 16000);
            IWaveProvider waveProvider = new SampleToWaveProvider16(sampleProvider);
            capture.DataAvailable += HandleDataAvailable;
            capture.RecordingStopped += HandleRecordingStopped;
            capturePumpTask = Task.Run(() => PumpCaptureAsync(waveProvider, captureCancellationTokenSource.Token), captureCancellationTokenSource.Token);
            Volatile.Write(ref isRecording, 1);

            try
            {
                capture.StartRecording();
                AssistantWakeDiagnostics.Write("Audio.Start.Completed", GetDiagnosticState());
            }
            catch (Exception exception)
            {
                Volatile.Write(ref isRecording, 0);
                AssistantWakeDiagnostics.Write("Audio.Start.Failed", $"{exception.GetType().Name}: {exception.Message}");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            AssistantWakeDiagnostics.Write("Audio.Dispose.Begin", GetDiagnosticState());
            await captureCancellationTokenSource.CancelAsync();

            if (capture is not null)
            {
                capture.DataAvailable -= HandleDataAvailable;
                capture.RecordingStopped -= HandleRecordingStopped;
                Volatile.Write(ref isRecording, 0);

                try
                {
                    capture.StopRecording();
                }
                catch (Exception)
                {
                }
            }

            if (capturePumpTask is not null)
            {
                try
                {
                    await capturePumpTask;
                }
                catch (Exception)
                {
                }
            }

            capture?.Dispose();
            captureCancellationTokenSource.Dispose();
            AssistantWakeDiagnostics.Write("Audio.Dispose.Completed", GetDiagnosticState());
        }

        private void HandleDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (args.BytesRecorded > 0)
            {
                _ = Interlocked.Increment(ref dataCallbackCount);
                _ = Interlocked.Add(ref dataBytes, args.BytesRecorded);
                _ = Interlocked.Exchange(ref lastDataTimestamp, DateTimeOffset.UtcNow.Ticks);
                sourceBuffer?.AddSamples(args.Buffer, 0, args.BytesRecorded);
            }
        }

        private void HandleRecordingStopped(object? sender, StoppedEventArgs args)
        {
            recordingFailure = args.Exception ?? new InvalidOperationException("Windows stopped assistant audio capture");
            Volatile.Write(ref isRecording, 0);
            AssistantWakeDiagnostics.Write("Audio.RecordingStopped", $"{recordingFailure.GetType().Name}: {recordingFailure.Message}; {GetDiagnosticState()}");
        }

        private async Task PumpCaptureAsync(IWaveProvider waveProvider, CancellationToken cancellationToken)
        {
            const int bufferDurationMilliseconds = 40;
            int bytesPerBuffer = 16000 * sizeof(short) * bufferDurationMilliseconds / 1000;
            byte[] readBuffer = new byte[bytesPerBuffer];
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(bufferDurationMilliseconds));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                int bytesRead = waveProvider.Read(readBuffer, 0, readBuffer.Length);

                if (bytesRead == 0)
                {
                    continue;
                }

                byte[] buffer = new byte[bytesRead];
                Buffer.BlockCopy(readBuffer, 0, buffer, 0, bytesRead);

                if (HasSpeechLikeLevel(buffer))
                {
                    _ = Interlocked.Exchange(ref lastSpeechLikeAudioTimestamp, DateTime.UtcNow.Ticks);
                }

                lock (gate)
                {
                    bufferedAudio.Enqueue(new BufferedAudioChunk(++audioSequence, buffer));

                    while (bufferedAudio.Count > BufferedChunkCount)
                    {
                        _ = bufferedAudio.Dequeue();
                    }

                }
            }
        }

        private static bool HasSpeechLikeLevel(byte[] buffer)
        {
            long magnitude = 0;
            int sampleCount = buffer.Length / sizeof(short);

            for (int index = 0; index < buffer.Length - 1; index += sizeof(short))
            {
                short sample = (short)(buffer[index] | buffer[index + 1] << 8);
                magnitude += Math.Abs((int)sample);
            }

            return sampleCount > 0 && magnitude / sampleCount >= 500;
        }

        private sealed record BufferedAudioChunk(long Sequence, byte[] Audio);

        private sealed class DownmixSampleProvider(ISampleProvider source) :
            ISampleProvider
        {
            private readonly ISampleProvider source = source;
            private readonly float[] sourceBuffer = new float[4096];

            public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

            public int Read(float[] buffer, int offset, int count)
            {
                int sourceChannels = source.WaveFormat.Channels;
                int sourceSamplesRead = source.Read(sourceBuffer, 0, Math.Min(sourceBuffer.Length, count * sourceChannels));
                int framesRead = sourceSamplesRead / sourceChannels;

                for (int frame = 0; frame < framesRead; frame++)
                {
                    float sum = 0;

                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        sum += sourceBuffer[(frame * sourceChannels) + channel];
                    }

                    buffer[offset + frame] = sum / sourceChannels;
                }

                return framesRead;
            }
        }
    }
}
