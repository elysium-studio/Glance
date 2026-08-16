using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Glance.Transcription;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceAssistantServiceTests
{
    [Fact]
    public void SettingsChangesAreDispatchedBeforePresentationPropertiesChange()
    {
        GlanceSettings settings = new() { IsAssistantEnabled = false };
        QueuedDispatcher dispatcher = new();
        GlanceAssistantService service = new(settings, new TestWritableOptions(settings), new WeakReferenceMessenger(), dispatcher, new TestActionService(), new TestTranscriptionModelCatalog(), NullLogger<GlanceAssistantService>.Instance);

        service.Receive(new OptionsChangedEventArgs<GlanceSettings>(new GlanceSettings { IsAssistantEnabled = true }));

        Assert.False(service.IsEnabled);
        dispatcher.Execute();
        Assert.True(service.IsEnabled);
    }

    private sealed class QueuedDispatcher :
        IDispatcher
    {
        private Action? action;

        public void Dispatch(Action action) => this.action = action;

        public void Execute()
        {
            Action? pendingAction = action;
            action = null;
            pendingAction?.Invoke();
        }
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(Action<GlanceSettings> update, CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }

        public Task WriteAsync(GlanceSettings value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestActionService :
        IGlanceActionService
    {
        public event EventHandler<GlanceActionPresentationRequestedEventArgs>? PresentationRequested
        {
            add { }
            remove { }
        }

        public event EventHandler<GlanceActionInvokedEventArgs>? ActionInvoked
        {
            add { }
            remove { }
        }

        public IReadOnlyList<GlanceActionDescriptor> GetActions() => [];

        public GlanceActionDescriptor? GetAction(string actionId) => null;

        public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(GlanceActionResult.Unavailable());
    }

    private sealed class TestTranscriptionModelCatalog :
        ITranscriptionModelCatalog
    {
        private static readonly TranscriptionModel Model = new("test", "Test", "Test", 0);

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<TranscriptionModel> Models => [Model];

        public string DefaultModelId => Model.Id;

        public bool IsInstalled(string modelId) => modelId == Model.Id;

        public Task<TranscriptionModelState> GetStateAsync(string modelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TranscriptionModelState.Installed);

        public Task InstallAsync(string modelId,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(string modelId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public TranscriptionModelDownload? GetDownload(string modelId) => null;

        public bool CancelInstall(string modelId) => false;
    }
}
