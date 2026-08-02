namespace Glance.Application.Abstractions;

public enum GlanceAssistantState
{
    Disabled,
    Preparing,
    ListeningForWakeWord,
    ListeningForCommand,
    ProcessingCommand,
    Error
}
