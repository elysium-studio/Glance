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
        GlanceQuickConverterRegistry registry = new();
        registry.Register([unsupported, supported, exact]);
        GlanceContentContext context = new(GlanceContentKind.Text, [], "https://example.com/media");

        IReadOnlyList<IGlanceQuickConverter> matches = registry.GetConverters(context);

        Assert.Equal(1, unsupported.MatchCount);
        Assert.Equal(1, supported.MatchCount);
        Assert.Equal(1, exact.MatchCount);
        Assert.Equal([exact, supported], matches);
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
