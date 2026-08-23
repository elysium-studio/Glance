namespace Glance.Shell;

public interface IGlanceModuleFeedValidator
{
    void Validate(GlanceModuleFeed feed, GlanceModuleFeedSource source);
}
