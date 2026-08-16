using Glance.Application.Abstractions;
using Glance.Transcription;
using Glance.UI.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.SpeechToText.WinUI;

public sealed class SpeechToTextComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IAsyncDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly IAudioInputSourceCatalog audioSourceCatalog;
    private readonly ITranscriptionModelCatalog modelCatalog;
    private readonly ITranscriptionModelSelection modelSelection;
    private readonly ITranscriptionSessionFactory sessionFactory;
    private readonly ITextCopyService textCopyService;
    private readonly SpeechToTextViewModel viewModel;
    private readonly ModuleResourceTextLocalizer<SpeechToTextModule> localizer;
    private readonly ILogger<SpeechToTextComponent> logger;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private readonly Task initialization;

    private ITranscriptionSession? session;
    private Task? resultReader;
    private string? sessionAudioSourceId;
    private string? modelId;
    private bool disposed;

    public SpeechToTextComponent(SpeechToTextViewModel viewModel,
        IAudioInputSourceCatalog audioSourceCatalog,
        ITranscriptionModelCatalog modelCatalog,
        ITranscriptionModelSelection modelSelection,
        ITranscriptionSessionFactory sessionFactory,
        ITextCopyService textCopyService,
        ModuleResourceTextLocalizer<SpeechToTextModule> localizer,
        ILogger<SpeechToTextComponent> logger)
    {
        this.viewModel = viewModel;
        this.audioSourceCatalog = audioSourceCatalog;
        this.modelCatalog = modelCatalog;
        this.modelSelection = modelSelection;
        this.sessionFactory = sessionFactory;
        this.textCopyService = textCopyService;
        this.localizer = localizer;
        this.logger = logger;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        SpeechToTextCompactView compactView = new(viewModel);
        SpeechToTextExpandedView expandedView = new(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ToggleListeningRequested += HandleToggleListeningRequested;
        viewModel.AudioSourceChanged += HandleAudioSourceChanged;
        viewModel.ClearRequested += HandleClearRequested;
        viewModel.CopyRequested += HandleCopyRequested;
        modelCatalog.StateChanged += HandleModelStateChanged;
        modelSelection.SelectionChanged += HandleModelSelectionChanged;
        initialization = InitializeAsync(lifetime.Token);
    }

    public string Id => "SpeechToText";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 95;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("SpeechToText.Start", Id, "Start speech transcription", "Start transcribing the selected microphone on this device."),
        new GlanceActionDescriptor("SpeechToText.Pause", Id, "Pause speech transcription", "Pause the current speech transcription session."),
        new GlanceActionDescriptor("SpeechToText.Resume", Id, "Resume speech transcription", "Resume the current speech transcription session.")
    ];

    public bool IsAvailable(string actionId) => actionId switch
    {
        "SpeechToText.Start" => viewModel.State == SpeechToTextState.Ready && viewModel.SelectedAudioSource is not null,
        "SpeechToText.Pause" => viewModel.State == SpeechToTextState.Listening,
        "SpeechToText.Resume" => viewModel.State == SpeechToTextState.Paused,
        _ => false
    };

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable(request.ActionId))
        {
            return GlanceActionResult.Unavailable();
        }

        await ToggleAsync(cancellationToken);
        return GlanceActionResult.Success();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        viewModel.ToggleListeningRequested -= HandleToggleListeningRequested;
        viewModel.AudioSourceChanged -= HandleAudioSourceChanged;
        viewModel.ClearRequested -= HandleClearRequested;
        viewModel.CopyRequested -= HandleCopyRequested;
        modelCatalog.StateChanged -= HandleModelStateChanged;
        modelSelection.SelectionChanged -= HandleModelSelectionChanged;
        lifetime.Cancel();

        try
        {
            await initialization.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            operationLock.Release();
            operationLock.Dispose();
            lifetime.Dispose();
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<AudioInputSource> sources = await audioSourceCatalog.GetSourcesAsync(cancellationToken).ConfigureAwait(false);
            modelId = TranscriptionModelResolver.ResolveInstalledModel(modelCatalog, modelSelection);
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                viewModel.SetAudioSources(sources);
                if (modelId is null)
                {
                    viewModel.SetModelRequired();
                }
                else
                {
                    viewModel.SetReady();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize speech transcription");
            _ = dispatcherQueue.TryEnqueue(() => viewModel.ShowError(localizer.GetText("InitializationFailed")));
        }
    }

    private void HandleToggleListeningRequested(object? sender, EventArgs args) => _ = ToggleAsync(lifetime.Token);

    private void HandleAudioSourceChanged(object? sender, AudioInputSource source)
    {
        if (session is not null && !string.Equals(sessionAudioSourceId, source.Id, StringComparison.OrdinalIgnoreCase))
        {
            _ = ResetSessionAsync();
        }
    }

    private void HandleClearRequested(object? sender, EventArgs args) => viewModel.ClearTranscript();

    private void HandleCopyRequested(object? sender, string text) => _ = textCopyService.CopyAsync(text);

    private void HandleModelStateChanged(object? sender, EventArgs args) => _ = RefreshModelAsync();

    private void HandleModelSelectionChanged(object? sender, EventArgs args) => _ = RefreshModelAsync();

    private async Task RefreshModelAsync()
    {
        await operationLock.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            string? installedModel = TranscriptionModelResolver.ResolveInstalledModel(modelCatalog, modelSelection);
            if (string.Equals(installedModel, modelId, StringComparison.Ordinal))
            {
                return;
            }

            modelId = installedModel;
            await DisposeSessionAsync().ConfigureAwait(false);
            _ = dispatcherQueue.TryEnqueue(() =>
            {
                if (modelId is null)
                {
                    viewModel.SetModelRequired();
                }
                else
                {
                    viewModel.SetReady();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task ToggleAsync(CancellationToken cancellationToken)
    {
        await initialization.ConfigureAwait(false);
        await operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (viewModel.State == SpeechToTextState.Listening && session is not null)
            {
                await session.PauseAsync(cancellationToken).ConfigureAwait(false);
                _ = dispatcherQueue.TryEnqueue(viewModel.PauseListening);
                return;
            }

            if (viewModel.State == SpeechToTextState.Paused && session is not null)
            {
                await session.ResumeAsync(cancellationToken).ConfigureAwait(false);
                _ = dispatcherQueue.TryEnqueue(viewModel.BeginListening);
                return;
            }

            AudioInputSource? source = viewModel.SelectedAudioSource;
            if (modelId is null || source is null)
            {
                return;
            }

            _ = dispatcherQueue.TryEnqueue(viewModel.BeginStarting);
            await DisposeSessionAsync().ConfigureAwait(false);
            TranscriptionSessionOptions options = new(modelId, source.Id);
            session = await Task.Run(() => sessionFactory.CreateAsync(options, cancellationToken), cancellationToken).ConfigureAwait(false);
            sessionAudioSourceId = source.Id;
            resultReader = ReadResultsAsync(session, lifetime.Token);
            _ = dispatcherQueue.TryEnqueue(viewModel.BeginListening);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Speech transcription failed");
            await DisposeSessionAsync().ConfigureAwait(false);
            _ = dispatcherQueue.TryEnqueue(() => viewModel.ShowError(localizer.GetText("TranscriptionFailed")));
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task ReadResultsAsync(ITranscriptionSession activeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (TranscriptionResult result in activeSession.GetResultsAsync(cancellationToken).ConfigureAwait(false))
            {
                _ = dispatcherQueue.TryEnqueue(() => viewModel.ApplyRecognition(result));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed while reading speech transcription results");
            _ = dispatcherQueue.TryEnqueue(() => viewModel.ShowError(localizer.GetText("TranscriptionFailed")));
        }
    }

    private async Task ResetSessionAsync()
    {
        await operationLock.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            await DisposeSessionAsync().ConfigureAwait(false);
            _ = dispatcherQueue.TryEnqueue(viewModel.SetReady);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task DisposeSessionAsync()
    {
        ITranscriptionSession? activeSession = session;
        Task? activeReader = resultReader;
        session = null;
        resultReader = null;
        sessionAudioSourceId = null;

        if (activeSession is null)
        {
            return;
        }

        try
        {
            await activeSession.StopAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        try
        {
            await activeSession.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        if (activeReader is not null)
        {
            try
            {
                await activeReader.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
