namespace Glance.Application.Abstractions;

public sealed record GlanceActionDescriptor(string Id,
    string TargetComponentId,
    string DisplayName,
    string Description,
    IReadOnlyList<GlanceActionParameterDescriptor> Parameters,
    GlanceActionConfirmation Confirmation = GlanceActionConfirmation.None,
    GlanceActionPresentation Presentation = GlanceActionPresentation.Compact)
{
    public GlanceActionDescriptor(string id,
        string targetComponentId,
        string displayName,
        string description,
        GlanceActionPresentation presentation = GlanceActionPresentation.Compact) :
        this(id, targetComponentId, displayName, description, [], GlanceActionConfirmation.None, presentation)
    { }
}
