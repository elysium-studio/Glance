namespace Glance.Application.Abstractions;

public enum GlanceActionStatus
{
    Succeeded,
    InvalidArguments,
    Unavailable,
    ConfirmationRequired,
    Failed,
    Cancelled
}
