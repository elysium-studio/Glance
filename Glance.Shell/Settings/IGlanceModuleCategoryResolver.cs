using Glance.Application.Abstractions;

namespace Glance.Shell;

public interface IGlanceModuleCategoryResolver
{
    GlanceModuleCategoryDescriptor Resolve(IGlanceComponent? component);

    GlanceModuleCategoryDescriptor Resolve(GlanceModuleFeedItem module);
}
