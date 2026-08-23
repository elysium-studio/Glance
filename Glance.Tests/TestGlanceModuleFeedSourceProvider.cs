using Glance.Shell;

namespace Glance.Tests;

internal sealed class TestGlanceModuleFeedSourceProvider(params GlanceModuleFeedSource[] sources) :
    IGlanceModuleFeedSourceProvider
{
    public IReadOnlyList<GlanceModuleFeedSource> GetSources() => sources;
}
