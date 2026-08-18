namespace Glance.Application.Abstractions;

public sealed record GlanceQuickConverterInstallResult(bool IsSuccessful, bool RequiresRestart, string? ErrorMessage)
{
    public static GlanceQuickConverterInstallResult Installed(bool requiresRestart) => new(true, requiresRestart, null);

    public static GlanceQuickConverterInstallResult Failed(string message) => new(false, false, message);
}
