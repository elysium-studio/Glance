using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceAssistantSemanticResolverServiceTests
{
    [Fact]
    public async Task TryExecuteAsyncInvokesTheResolvedAction()
    {
        using JsonDocument arguments = JsonDocument.Parse("{\"minutes\":24}");
        TestResolver resolver = new("Test", new GlanceAssistantActionResolution("Timer.Start", arguments.RootElement.Clone()));
        TestActionService actionService = new();
        GlanceSettings settings = new();
        GlanceAssistantSemanticResolverService service = new([resolver], actionService, settings, new TestWritableOptions(settings), NullLogger<GlanceAssistantSemanticResolverService>.Instance);

        GlanceAssistantCommandResult result = await service.TryExecuteAsync("set a time for twenty four minutes");

        Assert.True(result.Handled);
        Assert.Equal("Timer started", result.Response);
        Assert.Equal("Timer.Start", actionService.Request?.ActionId);
        Assert.Equal(24, actionService.Request?.GetNumber("minutes"));
    }

    [Fact]
    public async Task TryExecuteAsyncReturnsModuleRejectionForClarification()
    {
        using JsonDocument arguments = JsonDocument.Parse("{\"minutes\":24}");
        TestResolver resolver = new("Test", new GlanceAssistantActionResolution("Timer.Start", arguments.RootElement.Clone()));
        TestActionService actionService = new(GlanceActionResult.InvalidArguments("How long should the timer run?", "Say a duration such as 24 minutes."));
        GlanceSettings settings = new();
        GlanceAssistantSemanticResolverService service = new([resolver], actionService, settings, new TestWritableOptions(settings), NullLogger<GlanceAssistantSemanticResolverService>.Instance);

        GlanceAssistantCommandResult result = await service.TryExecuteAsync("set a timer");

        Assert.False(result.Handled);
        Assert.Equal("How long should the timer run?", result.Response);
        Assert.Equal("Say a duration such as 24 minutes.", result.Guidance);
    }

    [Fact]
    public async Task SetActiveResolverAsyncPersistsTheUserSelection()
    {
        TestResolver first = new("First", null);
        TestResolver second = new("Second", null);
        GlanceSettings settings = new();
        GlanceAssistantSemanticResolverService service = new([first, second], new TestActionService(), settings, new TestWritableOptions(settings), NullLogger<GlanceAssistantSemanticResolverService>.Instance);

        await service.SetActiveResolverAsync("Second");

        Assert.Same(second, service.ActiveResolver);
        Assert.Equal("Second", settings.AssistantSemanticResolverId);
    }

    [Fact]
    public void RegisterSelectsAHotLoadedPreferredResolver()
    {
        TestResolver fallback = new("Fallback", null);
        TestResolver preferred = new("Preferred", null);
        GlanceSettings settings = new() { AssistantSemanticResolverId = "Preferred" };
        GlanceAssistantSemanticResolverService service = new([fallback], new TestActionService(), settings, new TestWritableOptions(settings), NullLogger<GlanceAssistantSemanticResolverService>.Instance);

        service.Register([preferred]);

        Assert.Same(preferred, service.ActiveResolver);
    }

    private sealed class TestResolver(string id,
        GlanceAssistantActionResolution? resolution) :
        IGlanceAssistantSemanticResolver
    {
        public string Id => id;

        public string DisplayName => id;

        public Task<GlanceAssistantActionResolution?> ResolveAsync(string command,
            IReadOnlyList<GlanceActionDescriptor> actions,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(resolution);
    }

    private sealed class TestActionService(GlanceActionResult? result = null) :
        IGlanceActionService
    {
        private readonly GlanceActionDescriptor action = new("Timer.Start",
            "Timer",
            "Start timer",
            "Start a timer.",
            [new GlanceActionParameterDescriptor("minutes", GlanceActionParameterType.Number, "Duration in minutes")]);

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

        public GlanceActionRequest? Request { get; private set; }

        public IReadOnlyList<GlanceActionDescriptor> GetActions() => [action];

        public GlanceActionDescriptor? GetAction(string actionId) =>
            string.Equals(actionId, action.Id, StringComparison.OrdinalIgnoreCase) ? action : null;

        public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result ?? GlanceActionResult.Success("Timer started"));
        }
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(Action<GlanceSettings> update,
            CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }

        public Task WriteAsync(GlanceSettings value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
