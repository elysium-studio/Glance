using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceInspectorProviderRegistryTests
{
    [Fact]
    public void GetProvidersCombinesAndOrdersEveryMatch()
    {
        TestProvider unsupported = new("Unsupported", GlanceInspectorMatch.None);
        TestProvider supported = new("Supported", GlanceInspectorMatch.Supported);
        TestProvider exact = new("Exact", GlanceInspectorMatch.Exact);
        TestPreferences preferences = new();
        GlanceInspectorProviderRegistry registry = new(preferences);
        registry.Register(null, [unsupported, supported, exact]);
        GlanceContentContext context = new(GlanceContentKind.FilesAndFolders, [new GlanceStorageItem("C:\\sample.png", "sample.png", false)]);

        IReadOnlyList<IGlanceInspectorProvider> matches = registry.GetProviders(context);

        Assert.Equal(1, unsupported.MatchCount);
        Assert.Equal(1, supported.MatchCount);
        Assert.Equal(1, exact.MatchCount);
        Assert.Equal([exact, supported], matches);
    }

    [Fact]
    public async Task DisabledProvidersAreNotMatched()
    {
        TestProvider provider = new("Disabled", GlanceInspectorMatch.Exact);
        TestPreferences preferences = new();
        GlanceInspectorProviderRegistry registry = new(preferences);
        registry.Register(null, [provider]);
        await preferences.SetEnabledAsync(provider.Descriptor.Id, false);

        IReadOnlyList<IGlanceInspectorProvider> matches = registry.GetProviders(new GlanceContentContext(GlanceContentKind.FilesAndFolders, [new GlanceStorageItem("C:\\sample.png", "sample.png", false)]));

        Assert.Empty(matches);
        Assert.Equal(0, provider.MatchCount);

        await preferences.SetEnabledAsync(provider.Descriptor.Id, true);
        matches = registry.GetProviders(new GlanceContentContext(GlanceContentKind.FilesAndFolders, [new GlanceStorageItem("C:\\sample.png", "sample.png", false)]));

        Assert.Equal([provider], matches);
        Assert.Equal(1, provider.MatchCount);
    }

    private sealed class TestPreferences :
        IGlanceInspectorProviderPreferences
    {
        private readonly HashSet<string> disabled = [with(StringComparer.OrdinalIgnoreCase)];

        public bool IsEnabled(string providerId) => !disabled.Contains(providerId);

        public Task RegisterAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(IEnumerable<string> providerIds, CancellationToken cancellationToken = default)
        {
            disabled.ExceptWith(providerIds);
            return Task.CompletedTask;
        }

        public Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default)
        {
            if (enabled)
            {
                _ = disabled.Remove(providerId);
            }
            else
            {
                _ = disabled.Add(providerId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestProvider(string id, GlanceInspectorMatch match) :
        IGlanceInspectorProvider
    {
        public GlanceInspectorProviderDescriptor Descriptor { get; } = new(id, id, id);

        public int MatchCount { get; private set; }

        public GlanceInspectorMatch Match(GlanceContentContext context)
        {
            MatchCount++;
            return match;
        }

        public Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default) => Task.FromResult(GlanceInspectionResult.Empty);
    }
}
