using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using System.Text.Json;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceActionServiceTests
{
    [Fact]
    public void EveryEnabledComponentAdvertisesAShowAction()
    {
        GlanceActionService service = CreateService(new TestComponent("Weather"));

        GlanceActionDescriptor action = Assert.Single(service.GetActions());

        Assert.Equal("Weather.Show", action.Id);
        Assert.Equal(GlanceActionPresentation.Expanded, action.Presentation);
    }

    [Fact]
    public void DisabledComponentDoesNotAdvertiseActions()
    {
        GlanceSettings settings = new()
        {
            Modules = [new GlanceModulePreference { Id = "Timer", IsEnabled = false }]
        };
        ModulePreferenceService preferences = new([new TestComponent("Timer")], settings, new TestWritableOptions(settings));
        GlanceActionService service = new(preferences, new ImmediateDispatcher());
        service.Register([new TestActionProvider()]);

        Assert.Empty(service.GetActions());
    }

    [Fact]
    public async Task InvalidArgumentsAreRejectedBeforeInvocation()
    {
        GlanceActionService service = CreateService(new TestComponent("Timer"));
        TestActionProvider provider = new();
        service.Register([provider]);
        using JsonDocument document = JsonDocument.Parse("{\"minutes\":0}");

        GlanceActionResult result = await service.InvokeAsync(new GlanceActionRequest("Timer.Start", document.RootElement));

        Assert.Equal(GlanceActionStatus.InvalidArguments, result.Status);
        Assert.False(provider.WasInvoked);
    }

    [Fact]
    public async Task SuccessfulInvocationPresentsTheTargetComponent()
    {
        GlanceActionService service = CreateService(new TestComponent("Timer"));
        TestActionProvider provider = new();
        GlanceActionInvokedEventArgs? invoked = null;
        GlanceActionPresentationRequestedEventArgs? presentation = null;
        service.Register([provider]);
        service.ActionInvoked += (_, args) => invoked = args;
        service.PresentationRequested += (_, args) => presentation = args;
        using JsonDocument document = JsonDocument.Parse("{\"minutes\":24}");

        GlanceActionResult result = await service.InvokeAsync(new GlanceActionRequest("Timer.Start", document.RootElement));

        Assert.True(result.Succeeded);
        Assert.True(provider.WasInvoked);
        Assert.Equal("Timer", invoked?.TargetComponentId);
        Assert.Equal(GlanceActionPresentation.Compact, invoked?.Presentation);
        Assert.Equal("Timer", presentation?.TargetComponentId);
        Assert.Equal(GlanceActionPresentation.Compact, presentation?.Presentation);
    }

    [Fact]
    public async Task ModuleRejectionPreventsPresentationAndInvocation()
    {
        GlanceActionService service = CreateService(new TestComponent("Timer"));
        RejectingActionProvider provider = new();
        GlanceActionPresentationRequestedEventArgs? presentation = null;
        service.Register([provider]);
        service.PresentationRequested += (_, args) => presentation = args;
        using JsonDocument document = JsonDocument.Parse("{\"minutes\":24}");

        GlanceActionResult result = await service.InvokeAsync(new GlanceActionRequest("Timer.Start", document.RootElement));

        Assert.Equal(GlanceActionStatus.InvalidArguments, result.Status);
        Assert.Equal("That timer request is ambiguous.", result.Message);
        Assert.True(provider.WasValidated);
        Assert.False(provider.WasInvoked);
        Assert.Null(presentation);
    }

    [Fact]
    public async Task UnavailableActionsRemainUnderstandableButCannotRun()
    {
        GlanceActionService service = CreateService(new TestComponent("Timer"));
        TestActionProvider provider = new() { Available = false };
        GlanceActionPresentationRequestedEventArgs? presentation = null;
        service.Register([provider]);
        service.PresentationRequested += (_, args) => presentation = args;
        using JsonDocument document = JsonDocument.Parse("{\"minutes\":24}");

        Assert.Contains(service.GetActions(), action => action.Id == "Timer.Start");

        GlanceActionResult result = await service.InvokeAsync(new GlanceActionRequest("Timer.Start", document.RootElement));

        Assert.Equal(GlanceActionStatus.Unavailable, result.Status);
        Assert.Equal("That action is not available in the module's current state.", result.Message);
        Assert.False(provider.WasInvoked);
        Assert.Null(presentation);
    }

    private static GlanceActionService CreateService(IGlanceComponent component)
    {
        GlanceSettings settings = new();
        ModulePreferenceService preferences = new([component], settings, new TestWritableOptions(settings));
        return new GlanceActionService(preferences, new ImmediateDispatcher());
    }

    private sealed class TestActionProvider :
        IGlanceActionProvider
    {
        public bool Available { get; init; } = true;

        public bool WasInvoked { get; private set; }

        public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
            new GlanceActionDescriptor("Timer.Start",
                "Timer",
                "Start timer",
                "Start a timer.",
                [new GlanceActionParameterDescriptor("minutes", GlanceActionParameterType.Number, "Timer duration in minutes.", Minimum: 1, Maximum: 1440)])
        ];

        public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
            CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            return Task.FromResult(GlanceActionResult.Success());
        }

        public bool IsAvailable(string actionId) => Available;
    }

    private sealed class TestComponent(string id) :
        IGlanceComponent
    {
        public string Id { get; } = id;

        public string DisplayName => Id;

        public string Description => string.Empty;

        public int Order => 0;

        public object CompactContent { get; } = new();

        public object ExpandedContent { get; } = new();
    }

    private sealed class RejectingActionProvider :
        IGlanceActionProvider,
        IGlanceActionValidator
    {
        public bool WasInvoked { get; private set; }

        public bool WasValidated { get; private set; }

        public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
            new GlanceActionDescriptor("Timer.Start",
                "Timer",
                "Start timer",
                "Start a timer.",
                [new GlanceActionParameterDescriptor("minutes", GlanceActionParameterType.Number, "Timer duration in minutes.")],
                Presentation: GlanceActionPresentation.Compact)
        ];

        public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
            CancellationToken cancellationToken = default)
        {
            WasValidated = true;
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("That timer request is ambiguous."));
        }

        public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
            CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            return Task.FromResult(GlanceActionResult.Success());
        }
    }

    private sealed class ImmediateDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }

    private sealed class TestWritableOptions(GlanceSettings settings) :
        IWritableOptions<GlanceSettings>
    {
        public Task<GlanceSettings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<GlanceSettings?>(settings);

        public Task WriteAsync(Action<GlanceSettings> update,
            CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }

        public Task WriteAsync(GlanceSettings value,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
