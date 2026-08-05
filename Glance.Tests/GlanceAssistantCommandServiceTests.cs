using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceAssistantCommandServiceTests
{
    [Fact]
    public async Task ExecuteAsyncUsesSemanticResolutionBeforeLegacyHandlers()
    {
        TestHandler handler = new(20, new GlanceAssistantCommandResult(true, "legacy"));
        TestSemanticResolverService semanticResolver = new(new GlanceAssistantCommandResult(true, "semantic"));
        GlanceAssistantCommandService service = new([handler], semanticResolver);

        GlanceAssistantCommandResult result = await service.ExecuteAsync("set a time for twenty four minutes");

        Assert.True(result.Handled);
        Assert.Equal("semantic", result.Response);
        Assert.Equal(1, semanticResolver.InvocationCount);
        Assert.Equal(0, handler.InvocationCount);
    }

    [Fact]
    public async Task ExecuteAsyncUsesTheHighestPriorityHandler()
    {
        TestHandler lowerPriority = new(10, new GlanceAssistantCommandResult(true, "lower"));
        TestHandler higherPriority = new(20, new GlanceAssistantCommandResult(true, "higher"));
        GlanceAssistantCommandService service = new([lowerPriority, higherPriority]);

        GlanceAssistantCommandResult result = await service.ExecuteAsync("test");

        Assert.True(result.Handled);
        Assert.Equal("higher", result.Response);
        Assert.Equal(0, lowerPriority.InvocationCount);
        Assert.Equal(1, higherPriority.InvocationCount);
    }

    [Fact]
    public async Task ExecuteAsyncContinuesUntilAHandlerAcceptsTheCommand()
    {
        TestHandler first = new(20, GlanceAssistantCommandResult.NotHandled);
        TestHandler second = new(10, new GlanceAssistantCommandResult(true, "done"));
        GlanceAssistantCommandService service = new([first, second]);

        GlanceAssistantCommandResult result = await service.ExecuteAsync("test");

        Assert.True(result.Handled);
        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(1, second.InvocationCount);
    }

    [Fact]
    public async Task RegisterMakesAHotLoadedHandlerAvailable()
    {
        GlanceAssistantCommandService service = new([]);
        TestHandler handler = new(10, new GlanceAssistantCommandResult(true, "loaded"));

        service.Register([handler]);
        GlanceAssistantCommandResult result = await service.ExecuteAsync("test");

        Assert.True(result.Handled);
        Assert.Equal("loaded", result.Response);
    }

    private sealed class TestHandler(int priority,
        GlanceAssistantCommandResult result) :
        IGlanceAssistantCommandHandler
    {
        public int Priority => priority;

        public int InvocationCount { get; private set; }

        public Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class TestSemanticResolverService(GlanceAssistantCommandResult result) :
        IGlanceAssistantSemanticResolverService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<IGlanceAssistantSemanticResolver> Resolvers => [];

        public IGlanceAssistantSemanticResolver? ActiveResolver => null;

        public int InvocationCount { get; private set; }

        public Task SetActiveResolverAsync(string resolverId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<GlanceAssistantCommandResult> TryExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(result);
        }
    }
}
