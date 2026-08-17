using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceIntentServiceTests
{
    [Fact]
    public void AdvertisedIntentIsAvailableForSupportedContent()
    {
        GlanceIntentService service = CreateService(new TestComponent("Stash"));
        TestIntent intent = new("Stash.Share", "Stash", GlanceContentKind.Text);
        service.Register([intent]);

        Assert.Equal(intent.Descriptor, Assert.Single(service.GetIntents(GlanceContentKind.Text)));
        Assert.Empty(service.GetIntents(GlanceContentKind.FilesAndFolders));
    }

    [Fact]
    public void DisabledTargetDoesNotAdvertiseItsIntent()
    {
        GlanceSettings settings = new()
        {
            Modules = [new GlanceModulePreference { Id = "Stash", IsEnabled = false }]
        };
        ModulePreferenceService preferences = new([new TestComponent("Stash")], settings, new TestWritableOptions(settings));
        GlanceIntentService service = new(preferences);
        service.Register([new TestIntent("Stash.Share", "Stash", GlanceContentKind.Text)]);

        Assert.Empty(service.GetIntents(GlanceContentKind.Text));
    }

    [Fact]
    public async Task InvokingIntentDeliversContentAndPresentsItsTarget()
    {
        GlanceIntentService service = CreateService(new TestComponent("Stash"));
        TestIntent intent = new("Stash.Share", "Stash", GlanceContentKind.Text);
        GlanceIntentInvokedEventArgs? invoked = null;
        service.Register([intent]);
        service.IntentInvoked += (_, args) => invoked = args;
        GlanceContentContext context = new(GlanceContentKind.Text, [], "Selected text");

        bool result = await service.InvokeAsync(intent.Descriptor.Id, context);

        Assert.True(result);
        Assert.Same(context, intent.Context);
        Assert.Equal("Stash", invoked?.TargetComponentId);
    }

    [Fact]
    public async Task CancellingIntentDoesNotPresentItsTarget()
    {
        GlanceIntentService service = CreateService(new TestComponent("Torrent"));
        CancelledIntent intent = new("Torrent.Add", "Torrent", GlanceContentKind.WebLink);
        GlanceIntentInvokedEventArgs? invoked = null;
        service.Register([intent]);
        service.IntentInvoked += (_, args) => invoked = args;
        GlanceContentContext context = new(GlanceContentKind.WebLink, [], "magnet:?xt=urn:btih:test");

        bool result = await service.InvokeAsync(intent.Descriptor.Id, context);

        Assert.False(result);
        Assert.Null(invoked);
    }

    [Fact]
    public void DescriptorRetainsModuleBinaryConstructorContract()
    {
        Type[] parameterTypes = [.. Enumerable.Repeat(typeof(string), 6)];

        Assert.NotNull(typeof(GlanceIntentDescriptor).GetConstructor(parameterTypes));
    }

    [Fact]
    public void ContextReturnsEveryCompatibleRouteForTheSelector()
    {
        GlanceIntentService service = CreateService(new TestComponent("Torrent"));
        TestIntent torrent = new("Torrent.Add", "Torrent", GlanceContentKind.FilesAndFolders);
        TestIntent shelf = new("DropShelf.Share", "Torrent", GlanceContentKind.FilesAndFolders);
        service.Register([torrent, shelf]);
        GlanceContentContext context = new(GlanceContentKind.FilesAndFolders,
            [new GlanceStorageItem("sample.torrent", "sample.torrent", false)]);

        Assert.Equal(2, service.GetIntents(context).Count);
    }

    private static GlanceIntentService CreateService(IGlanceComponent component)
    {
        GlanceSettings settings = new();
        ModulePreferenceService preferences = new([component], settings, new TestWritableOptions(settings));
        return new GlanceIntentService(preferences);
    }

    private sealed class TestIntent(string id,
        string targetComponentId,
        GlanceContentKind kind) :
        IGlanceIntent
    {
        public GlanceIntentDescriptor Descriptor { get; } = new(id, targetComponentId, id, id, "\uE718");

        public GlanceContentContext? Context { get; private set; }

        public bool CanHandle(GlanceContentKind value) => value == kind;

        public Task InvokeAsync(GlanceContentContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            return Task.CompletedTask;
        }
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

    private sealed class CancelledIntent(string id,
        string targetComponentId,
        GlanceContentKind kind) :
        IGlanceIntent
    {
        public GlanceIntentDescriptor Descriptor { get; } = new(id, targetComponentId, id, id, "\uE718");

        public bool CanHandle(GlanceContentKind value) => value == kind;

        public Task InvokeAsync(GlanceContentContext context,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryInvokeAsync(GlanceContentContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
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
