namespace Glance.Application.Abstractions;

public interface IGlanceTransientComponent :
    IGlanceComponent
{
    bool IsPresentationEnabled { get; set; }

    event EventHandler<GlanceTransientPresentationRequestedEventArgs>? PresentationRequested;

    event EventHandler? DismissalRequested;
}

public sealed class GlanceTransientPresentationRequestedEventArgs(bool expand = false) :
    EventArgs
{
    public bool Expand { get; } = expand;
}
