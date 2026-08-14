namespace Glance.Shell;

public interface IConditionalGlanceViewModel :
    IGlanceViewModel
{
    bool IsAvailable(GlanceSettings settings);
}
