namespace Glance.Application.Abstractions;

public sealed record GlanceAssistantCommandResult(bool Handled,
    string? Response = null)
{
    public static GlanceAssistantCommandResult NotHandled { get; } = new(false);
}
