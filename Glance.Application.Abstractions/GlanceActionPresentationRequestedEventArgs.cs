namespace Glance.Application.Abstractions;

public sealed class GlanceActionPresentationRequestedEventArgs(string targetComponentId,
    GlanceActionPresentation presentation) :
    EventArgs
{
    public string TargetComponentId { get; } = targetComponentId;

    public GlanceActionPresentation Presentation { get; } = presentation;
}
