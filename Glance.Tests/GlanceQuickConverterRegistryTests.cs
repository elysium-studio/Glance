using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceQuickConverterRegistryTests
{
    [Fact]
    public void GetConvertersAsksEveryProviderAndOrdersMatches()
    {
        TestConverter unsupported = new("Unsupported", GlanceQuickConverterMatch.None);
        TestConverter supported = new("Supported", GlanceQuickConverterMatch.Supported);
        TestConverter exact = new("Exact", GlanceQuickConverterMatch.Exact);
        TestPreferences preferences = new();
        GlanceQuickConverterRegistry registry = new(preferences);
        registry.Register([unsupported, supported, exact]);
        GlanceContentContext context = new(GlanceContentKind.Text, [], "https://example.com/media");

        IReadOnlyList<IGlanceQuickConverter> matches = registry.GetConverters(context);

        Assert.Equal(1, unsupported.MatchCount);
        Assert.Equal(1, supported.MatchCount);
        Assert.Equal(1, exact.MatchCount);
        Assert.Equal([exact, supported], matches);
    }

    [Fact]
    public async Task DisabledConvertersAreNotMatched()
    {
        TestConverter converter = new("Disabled", GlanceQuickConverterMatch.Exact);
        TestPreferences preferences = new();
        GlanceQuickConverterRegistry registry = new(preferences);
        registry.Register([converter]);
        await preferences.SetEnabledAsync(converter.Descriptor.Id, false);

        IReadOnlyList<IGlanceQuickConverter> matches = registry.GetConverters(new GlanceContentContext(GlanceContentKind.Text, [], "https://example.com/media"));

        Assert.Empty(matches);
        Assert.Equal(0, converter.MatchCount);

        await preferences.SetEnabledAsync(converter.Descriptor.Id, true);
        matches = registry.GetConverters(new GlanceContentContext(GlanceContentKind.Text, [], "https://example.com/media"));

        Assert.Equal([converter], matches);
        Assert.Equal(1, converter.MatchCount);
    }

    private sealed class TestPreferences :
        IGlanceQuickConverterPreferences
    {
        private readonly HashSet<string> disabled = [with(StringComparer.OrdinalIgnoreCase)];

        public bool IsEnabled(string converterId) => !disabled.Contains(converterId);

        public Task RegisterAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(IEnumerable<string> converterIds, CancellationToken cancellationToken = default)
        {
            disabled.ExceptWith(converterIds);
            return Task.CompletedTask;
        }

        public Task SetEnabledAsync(string converterId, bool enabled, CancellationToken cancellationToken = default)
        {
            if (enabled)
            {
                _ = disabled.Remove(converterId);
            }
            else
            {
                _ = disabled.Add(converterId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestConverter(string id,
        GlanceQuickConverterMatch match) :
        IGlanceQuickConverter
    {
        public GlanceQuickConverterDescriptor Descriptor { get; } = new(id, id, id);

        public int MatchCount { get; private set; }

        public GlanceQuickConverterMatch Match(GlanceContentContext context)
        {
            MatchCount++;
            return match;
        }

        public IGlanceQuickConverterEditor? CreateEditor(GlanceContentContext context) => null;

        public Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
