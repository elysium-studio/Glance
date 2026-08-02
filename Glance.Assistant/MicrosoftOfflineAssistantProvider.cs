using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Foundry.Local.OpenAI;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;
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
    private const int RuntimeStartupAttempts = 3;
    private const int WakeRecognitionRestartAttempts = 3;
    private const int UtteranceSilenceMilliseconds = 1800;
    private const string ModelAlias = "nemotron-speech-streaming-en-0.6b";
    private static readonly object nativeLibraryGate = new();
    private static readonly List<nint> nativeLibraryHandles = [];
    private static bool nativeLibrariesLoaded;
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
    private SpeechContinuousRecognitionSession? wakeRecognitionSession;
    private Task? modelPreparationTask;
    private Task? transcriptionTask;
    private Task? wakeHealthTask;
    private bool isStarted;
    private int commandSession;
    private int isReturningToWakeRecognition;
    private int isSwitchingToCommandRecognition;
    private int utteranceSession;

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
                if (State == GlanceAssistantState.Error)
                {
                    await StopAsync();
                }

                if (isStarted)
                {
                    if (providerCancellationTokenSource?.IsCancellationRequested == false &&
                        wakeHealthTask?.IsCompleted == false)
                    {
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
            isStarted = false;
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

            cancellationToken.ThrowIfCancellationRequested();
            providerCancellationTokenSource = new CancellationTokenSource();
            await StartAudioCaptureWithRetryAsync(providerCancellationTokenSource.Token);
            await StartWakeRecognitionWithRetryAsync();
            wakeHealthTask = MonitorWakeRecognitionAsync(providerCancellationTokenSource.Token);

            if (audioClient is null)
            {
                SetPresentation(GlanceAssistantState.Preparing, "Getting voice commands ready", "Loading the command model");
                modelPreparationTask = PrepareModelAsync(providerCancellationTokenSource.Token);
            }
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
        try
        {
            EnsureNativeLibrariesLoaded();
            await FoundryLocalManager.CreateAsync(new Configuration { AppName = "Glance" }, logger);
            ICatalog catalog = await FoundryLocalManager.Instance.GetCatalogAsync();
            model = await catalog.GetModelAsync(ModelAlias) ?? throw new InvalidOperationException("The Microsoft streaming speech model is unavailable");
            await model.DownloadAsync(_ => { }, cancellationToken);
            await model.LoadAsync(cancellationToken);
            audioClient = await model.GetAudioClientAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (State != GlanceAssistantState.Preparing ||
                Volatile.Read(ref isSwitchingToCommandRecognition) != 0 ||
                Volatile.Read(ref isReturningToWakeRecognition) != 0)
            {
                return;
            }
            SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", "Listening");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to prepare the Microsoft offline speech model");
            await RunOnDispatcherAsync(StopWakeRecognitionAsync);
            SetPresentation(GlanceAssistantState.Error, "Voice assistant unavailable", exception.Message);
        }
    }

    private async Task StopAsync()
    {
        isStarted = false;
        Interlocked.Exchange(ref isSwitchingToCommandRecognition, 0);
        CancelPendingUtterance();

        if (providerCancellationTokenSource is not null)
        {
            await providerCancellationTokenSource.CancelAsync();
        }

        await RunOnDispatcherAsync(StopWakeRecognitionAsync);
        await StopCommandRecognitionAsync();

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

        if (modelPreparationTask is not null)
        {
            try
            {
                await modelPreparationTask;
            }
            catch (OperationCanceledException)
            {
            }

            modelPreparationTask = null;
        }

        if (audioCapture is not null)
        {
            await audioCapture.DisposeAsync();
            audioCapture = null;
        }

        providerCancellationTokenSource?.Dispose();
        providerCancellationTokenSource = null;
    }

    private async Task StartWakeRecognitionAsync()
    {
        if (wakeRecognizer is not null)
        {
            return;
        }

        SpeechRecognizer recognizer = new(SpeechRecognizer.SystemSpeechLanguage);
        recognizer.Constraints.Add(new SpeechRecognitionListConstraint((string[])["Glance", "Hey Glance"], "GlanceWakePhrase"));
        SpeechRecognitionCompilationResult compilation = await recognizer.CompileConstraintsAsync();

        if (compilation.Status != SpeechRecognitionResultStatus.Success)
        {
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
            await session.StartAsync();
        }
        catch
        {
            wakeRecognizer = null;
            wakeRecognitionSession = null;
            session.ResultGenerated -= HandleWakeResultGenerated;
            session.Completed -= HandleWakeRecognitionCompleted;
            recognizer.Dispose();
            throw;
        }

        SetPresentation(audioClient is null ? GlanceAssistantState.Preparing : GlanceAssistantState.ListeningForWakeWord,
            "Say “Glance” or “Hey Glance”",
            audioClient is null ? "Loading the command model" : "Listening");
    }

    private async Task StopWakeRecognitionAsync()
    {
        SpeechRecognizer? recognizer = wakeRecognizer;
        SpeechContinuousRecognitionSession? session = wakeRecognitionSession;
        wakeRecognizer = null;
        wakeRecognitionSession = null;

        if (recognizer is null || session is null)
        {
            return;
        }

        session.ResultGenerated -= HandleWakeResultGenerated;
        session.Completed -= HandleWakeRecognitionCompleted;

        try
        {
            await session.CancelAsync();
        }
        catch (Exception)
        {
        }

        recognizer.Dispose();
    }

    private void HandleWakeResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (!ReferenceEquals(sender, wakeRecognitionSession))
        {
            return;
        }

        logger.LogInformation("Wake recognition heard {WakeText} with {WakeConfidence} confidence", args.Result.Text, args.Result.Confidence);

        if (args.Result.Confidence == SpeechRecognitionConfidence.Rejected)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref isSwitchingToCommandRecognition, 1, 0) != 0)
        {
            return;
        }

        long wakeBoundary = audioCapture?.CreateCheckpoint() ?? 0;
        Dispatch(() => _ = SwitchToCommandRecognitionAsync(args.Result.Text, wakeBoundary));
    }

    private void HandleWakeRecognitionCompleted(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        if (!ReferenceEquals(sender, wakeRecognitionSession))
        {
            return;
        }

        logger.LogWarning("Wake recognition completed unexpectedly with {WakeStatus}", args.Status);
        Dispatch(() => _ = RecoverWakeRecognitionAsync());
    }

    private async Task RecoverWakeRecognitionAsync()
    {
        if (providerCancellationTokenSource?.IsCancellationRequested != false ||
            Volatile.Read(ref isSwitchingToCommandRecognition) != 0 ||
            Interlocked.CompareExchange(ref isReturningToWakeRecognition, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await Task.Delay(250, providerCancellationTokenSource.Token);
            await StartWakeRecognitionWithRetryAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to restart wake recognition");
            SetPresentation(GlanceAssistantState.Preparing, "Voice assistant is recovering", "Restarting wake recognition");
        }
        finally
        {
            Interlocked.Exchange(ref isReturningToWakeRecognition, 0);
        }
    }

    private async Task MonitorWakeRecognitionAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                if (State is not (GlanceAssistantState.Preparing or GlanceAssistantState.ListeningForWakeWord) ||
                    Volatile.Read(ref isSwitchingToCommandRecognition) != 0 ||
                    Volatile.Read(ref isReturningToWakeRecognition) != 0)
                {
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
                    SpeechRecognizerState? recognizerState = null;
                    await RunOnDispatcherAsync(() =>
                    {
                        recognizerState = wakeRecognizer?.State;
                        restartRequired = wakeRecognizer is null ||
                            wakeRecognitionSession is null ||
                            recognizerState is SpeechRecognizerState.Idle or SpeechRecognizerState.Paused or SpeechRecognizerState.Processing;
                        return Task.CompletedTask;
                    });

                    if (restartRequired)
                    {
                        logger.LogWarning("Wake recognition became inactive in state {WakeRecognizerState} and will be restarted", recognizerState);
                        await RecoverWakeRecognitionAsync();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
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

    private static void EnsureNativeLibrariesLoaded()
    {
        lock (nativeLibraryGate)
        {
            if (nativeLibrariesLoaded)
            {
                return;
            }

            string directory = Path.GetDirectoryName(typeof(MicrosoftOfflineAssistantProvider).Assembly.Location) ?? AppContext.BaseDirectory;
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            if (!currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", $"{directory}{Path.PathSeparator}{currentPath}");
            }

            foreach (string fileName in (string[])["onnxruntime_providers_shared.dll", "onnxruntime.dll", "onnxruntime-genai.dll"])
            {
                string path = Path.Combine(directory, fileName);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"The assistant runtime dependency {fileName} was not found", path);
                }

                nint handle = LoadLibraryEx(path, 0, 0x00000100 | 0x00001000);

                if (handle == 0)
                {
                    throw new DllNotFoundException($"Unable to load {fileName} from the assistant package. Windows error {Marshal.GetLastWin32Error()}");
                }

                nativeLibraryHandles.Add(handle);
            }

            nativeLibrariesLoaded = true;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryEx(string fileName, nint file, uint flags);

    private async Task SwitchToCommandRecognitionAsync(string wakePhrase, long wakeBoundary)
    {
        try
        {
            if (audioCapture is null)
            {
                logger.LogWarning("Wake phrase {WakePhrase} was accepted without an active audio capture", wakePhrase);
                return;
            }

            if (wakeRecognizer is null)
            {
                logger.LogWarning("Wake phrase {WakePhrase} was accepted after its recognition session had already ended", wakePhrase);
                await ReturnToWakeRecognitionAsync("Listening");
                return;
            }

            OpenAIAudioClient? client = audioClient;

            if (client is null)
            {
                SetPresentation(GlanceAssistantState.ListeningForCommand, "I heard you", "Voice commands are still getting ready");
                await Task.Delay(900, providerCancellationTokenSource!.Token);

                if (audioClient is null)
                {
                    SetPresentation(GlanceAssistantState.Preparing, "Say “Glance” or “Hey Glance”", "Loading the command model");
                    return;
                }

                client = audioClient;
            }

            await RunOnDispatcherAsync(StopWakeRecognitionAsync);
            listeningCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
            CancellationToken cancellationToken = listeningCancellationTokenSource.Token;
            transcriptionSession = client.CreateLiveTranscriptionSession();
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

            if (providerCancellationTokenSource?.IsCancellationRequested == false)
            {
                await ReturnToWakeRecognitionAsync("Listening");
            }
        }
        finally
        {
            Interlocked.Exchange(ref isSwitchingToCommandRecognition, 0);
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
        if (State != GlanceAssistantState.ListeningForCommand)
        {
            return;
        }

        commandStartCancellationTokenSource?.Cancel();

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

    private void BeginCommandWindow(string transcript = "What can I help with?",
        string status = "Listening for your command")
    {
        pendingUtterance.Clear();
        utteranceCompletionCancellationTokenSource?.Cancel();
        utteranceCompletionCancellationTokenSource?.Dispose();
        utteranceCompletionCancellationTokenSource = null;
        utteranceSession++;
        commandStartCancellationTokenSource?.Cancel();
        commandStartCancellationTokenSource?.Dispose();
        commandStartCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(providerCancellationTokenSource!.Token);
        int session = ++commandSession;
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

            if (!result.Handled)
            {
                await PromptForAnotherCommandAsync(cancellationToken);
                return;
            }

            SetPresentation(GlanceAssistantState.ProcessingCommand,
                string.IsNullOrWhiteSpace(result.Response) ? command : result.Response,
                "Done");
            await Task.Delay(700, cancellationToken);
            await ReturnToWakeRecognitionAsync("Listening");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to execute assistant command {AssistantCommand}", command);

            if (providerCancellationTokenSource?.IsCancellationRequested == false)
            {
                await PromptForAnotherCommandAsync(providerCancellationTokenSource.Token);
            }
        }
    }

    private async Task PromptForAnotherCommandAsync(CancellationToken cancellationToken)
    {
        if (transcriptionSession is not null && listeningCancellationTokenSource?.IsCancellationRequested == false)
        {
            BeginCommandWindow("I didn't understand that, try again", "Listening for command");
            return;
        }

        await ReturnToWakeRecognitionAsync("Listening");
    }

    private async Task ReturnToWakeRecognitionAsync(string status)
    {
        if (Interlocked.CompareExchange(ref isReturningToWakeRecognition, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await StopCommandRecognitionAsync();

            if (providerCancellationTokenSource?.IsCancellationRequested != false)
            {
                return;
            }

            await StartWakeRecognitionWithRetryAsync();
            SetPresentation(GlanceAssistantState.ListeningForWakeWord, "Say “Glance” or “Hey Glance”", status);
        }
        catch (OperationCanceledException) when (providerCancellationTokenSource?.IsCancellationRequested != false)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to return to wake recognition");
            SetPresentation(GlanceAssistantState.Preparing, "Voice assistant is recovering", "Restarting wake recognition");
        }
        finally
        {
            Interlocked.Exchange(ref isReturningToWakeRecognition, 0);
        }
    }

    private async Task StartWakeRecognitionWithRetryAsync()
    {
        Exception? failure = null;

        for (int attempt = 1; attempt <= WakeRecognitionRestartAttempts; attempt++)
        {
            try
            {
                await RunOnDispatcherAsync(async () =>
                {
                    await StopWakeRecognitionAsync();
                    await StartWakeRecognitionAsync();
                });
                return;
            }
            catch (OperationCanceledException) when (providerCancellationTokenSource?.IsCancellationRequested != false)
            {
                throw;
            }
            catch (Exception exception)
            {
                failure = exception;
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
        CancelPendingUtterance();
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

    private Task RunOnDispatcherAsync(Func<Task> action)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Dispatch(async () =>
        {
            try
            {
                await action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
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
        private CancellationToken activeSessionCancellationToken;
        private LiveAudioTranscriptionSession? activeSession;
        private long audioSequence;
        private WasapiCapture? capture;
        private Task? capturePumpTask;
        private bool isAttaching;
        private Exception? recordingFailure;
        private int isRecording;
        private BufferedWaveProvider? sourceBuffer;

        public Exception? Failure => recordingFailure ?? capturePumpTask?.Exception?.GetBaseException();

        public bool IsHealthy => Volatile.Read(ref isRecording) != 0 && capturePumpTask?.IsCompleted == false;

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
            capture.RecordingStopped += HandleRecordingStopped;
            capturePumpTask = Task.Run(() => PumpCaptureAsync(waveProvider, captureCancellationTokenSource.Token), captureCancellationTokenSource.Token);
            Volatile.Write(ref isRecording, 1);

            try
            {
                capture.StartRecording();
            }
            catch
            {
                Volatile.Write(ref isRecording, 0);
                throw;
            }
        }

        public async Task AttachAsync(LiveAudioTranscriptionSession session, long wakeBoundary, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                isAttaching = true;
            }

            long replayedSequence = wakeBoundary;
            bool attached = false;
            using CancellationTokenSource attachmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attachmentCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
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
                            attached = true;
                            return;
                        }
                    }

                    foreach (BufferedAudioChunk chunk in snapshot)
                    {
                        await session.AppendAsync(chunk.Audio, attachmentCancellationTokenSource.Token);
                        replayedSequence = chunk.Sequence;
                    }
                }
            }
            finally
            {
                if (!attached)
                {
                    lock (gate)
                    {
                        isAttaching = false;
                    }
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
        }

        private void HandleDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (args.BytesRecorded > 0)
            {
                sourceBuffer?.AddSamples(args.Buffer, 0, args.BytesRecorded);
            }
        }

        private void HandleRecordingStopped(object? sender, StoppedEventArgs args)
        {
            recordingFailure = args.Exception ?? new InvalidOperationException("Windows stopped assistant audio capture");
            Volatile.Write(ref isRecording, 0);
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
                catch (Exception)
                {
                    lock (gate)
                    {
                        if (ReferenceEquals(activeSession, session))
                        {
                            activeSession = null;
                            activeSessionCancellationToken = default;
                        }
                    }
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
