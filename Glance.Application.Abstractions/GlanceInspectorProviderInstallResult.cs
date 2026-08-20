namespace Glance.Application.Abstractions;

public sealed record GlanceInspectorProviderInstallResult(bool IsSuccessful, bool RequiresRestart, string? ErrorMessage)
{
    public static GlanceInspectorProviderInstallResult Installed(bool requiresRestart) => new(true, requiresRestart, null);

    public static GlanceInspectorProviderInstallResult Failed(string errorMessage) => new(false, false, errorMessage);
}
