using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Foundry.Local.OpenAI;
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
    private const int UtteranceSilenceMilliseconds = 1800;
    private const string ModelAlias = "nemotron-speech-streaming-en-0.6b";
    private readonly IGlanceAssistantCommandService commandService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MicrosoftOfflineAssistantProvider> logger;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly StringBuilder pendingUtterance = new();
    private CancellationTokenSource? commandStartCancellationTokenSource;
    private CancellationTokenSource? listeningCancellationTokenSource;
    private CancellationTokenSource? providerCancellationTokenSource;
    private CancellationTokenSource? utteranceCompletionCancellationTokenSource;
    private IModel? model;
    private LiveAudioTranscriptionSession? transcriptionSession;
    private OpenAIAudioClient? audioClient;
    private RollingAudioCapture? audioCapture;
    private SpeechRecognizer? wakeRecognizer;
    private Task? transcriptionTask;
    private int commandSession;
    private int utteranceSession;
    private long pendingWakeBoundary;
    private string? pendingWakePhrase;

    [ObservableProperty]
    public partial GlanceAssistantState State { get; set; } = GlanceAssistantState.Disabled;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Voice assistant is off";

    [ObservableProperty]
    public partial string Transcript { get; set; } = "Say “Glance” or “Hey Glance”";

    public MicrosoftOfflineAssistantProvider(IGlanceAssistantCommandService commandService,
        IAssistantViewFactory viewFactory,
        IDispatcher dispatcher,
        ILogger<MicrosoftOfflineAssistantProvider> logger)
    {
        this.commandService = commandService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        CompactIndicatorContent = viewFactory.CreateCompactIndicator(this);
        ExpandedIndicatorContent = viewFactory.CreateExpandedIndicator(this);
        OverlayContent = viewFactory.CreateOverlay(this);
    }

    public string Id => "MicrosoftOffline";

    public string DisplayName => "Microsoft offline speech";

    public object CompactIndicatorContent { get; }

    public object ExpandedIndicatorContent { get; }

    public object OverlayContent { get; }

    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        await lifecycleGate.WaitAsync(cancellationToken);

        try
        {
            if (isEnabled)
            {
                if (State is not GlanceAssistantState.Disabled and not GlanceAssistantState.Error)
                {
                    return;
                }

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
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycleGate.WaitAsync();

        try
        {
            await StopAsync();

            if (model is not null)
            {
                await model.UnloadAsync();
                model = null;
                audioClient = null;
            }

            if (FoundryLocalManager.IsInitialized)
            {
                FoundryLocalManager.Instance.Dispose();
            }
        }
        finally
        {
            lifecycleGate.Release();
            lifecycleGate.Dispose();
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!HasPackageIdentity())
        {
            SetPresentation(GlanceAssistantState.Error, "Voice assistant unavailable", "Microsoft offline wake recognition requires packaged Glance");
            return;
        }

        try
        {
            AppCapabilityAccessStatus microphoneAccess = await AppCapability.Create("microphone").RequestAccessAsync();

            if (microphoneAccess != AppCapabilityAccessStatus.Allowed)
            {
                throw new UnauthorizedAccessException($"Microphone access is {microphoneAccess}");
            }

            providerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            audioCapture = new RollingAudioCapture(providerCancellationTokenSource.Token);
            audioCapture.Start();
            await StartWakeRecognitionAsync();
            _ = PrepareModelAsync(providerCancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to start the Microsoft offline assistant provider");
            await StopAsync();
            SetPresentation(GlanceAssistantState.Error, "Voice assistant unavailable", exception.Message);
        }
    }

    private async Task PrepareModelAsync(CancellationToken cancellationToken)
    {
        if (audioClient is not null)
        {
            return;
        }

        try
        {
            SetPresentation(GlanceAssistantState.Preparing, "Getting voice commands ready", "Wake-word listening is already active");
            await FoundryLocalManager.CreateAsync(new Configuration { AppName = "Glance" }, logger);
            await FoundryLocalManager.Instance.DownloadAndRegisterEpsAsync(cancellationToken);
            ICatalog catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
            model = await catalog.GetModelAsync(ModelAlias) ?? throw new InvalidOperationException("The Microsoft streaming speech model is unavailable");
            await model.DownloadAsync(_ => { }, cancellationToken);
            await model.LoadAsync(cancellationToken);
            audioClient = await model.GetAudioClientAsync();

            if (pendingWakePhrase is not null)
            {
                string wakePhrase = pendingWakePhrase;
                pendingWakePhrase = null;
                await SwitchToCommandRecognitionAsync(wakePhrase, pendingWakeBoundary);
            }
            else
            {
                SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", "Listening");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to prepare the Microsoft offline speech model");
            SetPresentation(GlanceAssistantState.Error, "Voice assistant unavailable", exception.Message);
        }
    }

    private async Task StopAsync()
    {
        CancelPendingUtterance();

        if (providerCancellationTokenSource is not null)
        {
            await providerCancellationTokenSource.CancelAsync();
        }

        await StopWakeRecognitionAsync();
        await StopCommandRecognitionAsync();

        if (audioCapture is not null)
        {
            await audioCapture.DisposeAsync();
            audioCapture = null;
        }

        providerCancellationTokenSource?.Dispose();
        providerCancellationTokenSource = null;
        pendingWakePhrase = null;
    }

    private async Task StartWakeRecognitionAsync()
    {
        SpeechRecognizer recognizer = new(SpeechRecognizer.SystemSpeechLanguage);
        recognizer.Constraints.Add(new SpeechRecognitionListConstraint((string[])["Glance", "Hey Glance"], "GlanceWakePhrase"));
        SpeechRecognitionCompilationResult compilation = await recognizer.CompileConstraintsAsync();

        if (compilation.Status != SpeechRecognitionResultStatus.Success)
        {
            recognizer.Dispose();
            throw new InvalidOperationException($"Windows could not compile the wake phrase: {compilation.Status}");
        }

        recognizer.ContinuousRecognitionSession.ResultGenerated += HandleWakeResultGenerated;
        await recognizer.ContinuousRecognitionSession.StartAsync();
        wakeRecognizer = recognizer;
        SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", audioClient is null ? "Listening while the command model gets ready" : "Listening");
    }

    private async Task StopWakeRecognitionAsync()
    {
        SpeechRecognizer? recognizer = wakeRecognizer;
        wakeRecognizer = null;

        if (recognizer is null)
        {
            return;
        }

        recognizer.ContinuousRecognitionSession.ResultGenerated -= HandleWakeResultGenerated;

        try
        {
            await recognizer.ContinuousRecognitionSession.CancelAsync();
        }
        catch (Exception)
        {
        }

        recognizer.Dispose();
    }

    private void HandleWakeResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Confidence == SpeechRecognitionConfidence.Rejected)
        {
            return;
        }

        long wakeBoundary = audioCapture?.CreateCheckpoint() ?? 0;
        Dispatch(() => _ = SwitchToCommandRecognitionAsync(args.Result.Text, wakeBoundary));
    }

    private async Task SwitchToCommandRecognitionAsync(string wakePhrase, long wakeBoundary)
    {
        if (wakeRecognizer is null || audioCapture is null)
        {
            return;
        }

        if (audioClient is null)
        {
            pendingWakePhrase = wakePhrase;
            pendingWakeBoundary = wakeBoundary;
            SetPresentation(GlanceAssistantState.ListeningForCommand, "I heard you", "Finishing command recognition setup");
            return;
        }

        try
        {
            await StopWakeRecognitionAsync();
            listeningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
            CancellationToken cancellationToken = listeningCancellationTokenSource.Token;
            transcriptionSession = audioClient.CreateLiveTranscriptionSession();
            transcriptionSession.Settings.SampleRate = 16000;
            transcriptionSession.Settings.Channels = 1;
            transcriptionSession.Settings.Language = "en";
            await transcriptionSession.StartAsync(cancellationToken);
            transcriptionTask = ReadTranscriptionAsync(transcriptionSession, cancellationToken);
            BeginCommandWindow();
            await audioCapture.AttachAsync(transcriptionSession, wakeBoundary, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to start assistant command recognition");
            await StopCommandRecognitionAsync();
            SetPresentation(GlanceAssistantState.Error, "Command recognition stopped", exception.Message);
        }
    }

    private async Task ReadTranscriptionAsync(LiveAudioTranscriptionSession session, CancellationToken cancellationToken)
    {
        await foreach (LiveAudioTranscriptionResponse result in session.GetStream(cancellationToken))
        {
            string text = result.Content?.FirstOrDefault()?.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(text))
            {
                Dispatch(() => ProcessRecognizedText(text));
            }
        }
    }

    private void ProcessRecognizedText(string text)
    {
        if (pendingUtterance.Length > 0 && !char.IsWhiteSpace(pendingUtterance[^1]) && !char.IsPunctuation(text[0]))
        {
            pendingUtterance.Append(' ');
        }

        pendingUtterance.Append(text);
        Transcript = pendingUtterance.ToString();
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
        int session = ++utteranceSession;
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
        pendingUtterance.Clear();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        _ = CompleteCommandAsync(command);
    }

    private void BeginCommandWindow()
    {
        commandStartCancellationTokenSource?.Cancel();
        commandStartCancellationTokenSource?.Dispose();
        commandStartCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
        int session = ++commandSession;
        SetPresentation(GlanceAssistantState.ListeningForCommand, "What can I help with?", "Listening for your command");
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
        try
        {
            CancellationToken cancellationToken = providerCancellationTokenSource?.Token ?? CancellationToken.None;
            commandStartCancellationTokenSource?.Cancel();
            SetPresentation(GlanceAssistantState.ProcessingCommand, command, "Working on it");
            GlanceAssistantCommandResult result = await commandService.ExecuteAsync(command, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.Response))
            {
                SetPresentation(GlanceAssistantState.ProcessingCommand, result.Response, result.Handled ? "Done" : "Command not recognised");
            }

            await Task.Delay(result.Handled ? 700 : 1300, cancellationToken);
            await ReturnToWakeRecognitionAsync(result.Handled ? "Listening" : "Try another command");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to execute assistant command {AssistantCommand}", command);
            SetPresentation(GlanceAssistantState.Error, "Command recognition stopped", exception.Message);
        }
    }

    private async Task ReturnToWakeRecognitionAsync(string status)
    {
        await StopCommandRecognitionAsync();

        if (providerCancellationTokenSource?.IsCancellationRequested != false)
        {
            return;
        }

        await StartWakeRecognitionAsync();
        SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", status);
    }

    private async Task StopCommandRecognitionAsync()
    {
        CancellationTokenSource? cancellationTokenSource = listeningCancellationTokenSource;
        listeningCancellationTokenSource = null;
        LiveAudioTranscriptionSession? session = transcriptionSession;
        transcriptionSession = null;
        Task? readingTask = transcriptionTask;
        transcriptionTask = null;
        audioCapture?.Detach();

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
    }

    private void CancelPendingUtterance()
    {
        commandSession++;
        commandStartCancellationTokenSource?.Cancel();
        commandStartCancellationTokenSource?.Dispose();
        commandStartCancellationTokenSource = null;
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        utteranceSession++;
        pendingUtterance.Clear();
    }

    private void SetPresentation(GlanceAssistantState state, string transcript, string status)
    {
        dispatcher.Dispatch(() =>
        {
            State = state;
            Transcript = transcript;
            StatusText = status;
        });
    }

    private void Dispatch(Action action) => dispatcher.Dispatch(action);

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
        private CancellationToken activeSessionCancellationToken;
        private LiveAudioTranscriptionSession? activeSession;
        private long audioSequence;
        private WasapiCapture? capture;
        private Task? capturePumpTask;
        private bool isAttaching;
        private BufferedWaveProvider? sourceBuffer;

        public long CreateCheckpoint()
        {
            lock (gate)
            {
                return audioSequence;
            }
        }

        public void Start()
        {
            capture = new WasapiCapture();
            sourceBuffer = new BufferedWaveProvider(capture.WaveFormat) { DiscardOnBufferOverflow = true, ReadFully = true };
            ISampleProvider sampleProvider = sourceBuffer.ToSampleProvider();
            sampleProvider = sampleProvider.WaveFormat.Channels == 1 ? sampleProvider : new DownmixSampleProvider(sampleProvider);
            sampleProvider = sampleProvider.WaveFormat.SampleRate == 16000 ? sampleProvider : new WdlResamplingSampleProvider(sampleProvider, 16000);
            IWaveProvider waveProvider = new SampleToWaveProvider16(sampleProvider);
            capture.DataAvailable += HandleDataAvailable;
            capturePumpTask = Task.Run(() => PumpCaptureAsync(waveProvider, captureCancellationTokenSource.Token), captureCancellationTokenSource.Token);
            capture.StartRecording();
        }

        public async Task AttachAsync(LiveAudioTranscriptionSession session, long wakeBoundary, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                isAttaching = true;
            }

            long replayedSequence = wakeBoundary;

            while (true)
            {
                BufferedAudioChunk[] snapshot;

                lock (gate)
                {
                    snapshot = [.. bufferedAudio.Where(chunk => chunk.Sequence > replayedSequence)];

                    if (snapshot.Length == 0)
                    {
                        activeSession = session;
                        activeSessionCancellationToken = cancellationToken;
                        isAttaching = false;
                        return;
                    }
                }

                foreach (BufferedAudioChunk chunk in snapshot)
                {
                    await session.AppendAsync(chunk.Audio, cancellationToken);
                    replayedSequence = chunk.Sequence;
                }
            }
        }

        public void Detach()
        {
            lock (gate)
            {
                activeSession = null;
                activeSessionCancellationToken = default;
                isAttaching = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await captureCancellationTokenSource.CancelAsync();

            if (capture is not null)
            {
                capture.DataAvailable -= HandleDataAvailable;

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
                catch (OperationCanceledException)
                {
                }
            }

            capture?.Dispose();
            captureCancellationTokenSource.Dispose();
        }

        private void HandleDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (args.BytesRecorded > 0)
            {
                sourceBuffer?.AddSamples(args.Buffer, 0, args.BytesRecorded);
            }
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
                LiveAudioTranscriptionSession? session;
                CancellationToken sessionCancellationToken;

                lock (gate)
                {
                    bufferedAudio.Enqueue(new BufferedAudioChunk(++audioSequence, buffer));

                    while (bufferedAudio.Count > BufferedChunkCount)
                    {
                        bufferedAudio.Dequeue();
                    }

                    session = isAttaching ? null : activeSession;
                    sessionCancellationToken = activeSessionCancellationToken;
                }

                if (session is null)
                {
                    continue;
                }

                try
                {
                    await session.AppendAsync(buffer, sessionCancellationToken);
                }
                catch (OperationCanceledException) when (sessionCancellationToken.IsCancellationRequested)
                {
                }
            }
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
