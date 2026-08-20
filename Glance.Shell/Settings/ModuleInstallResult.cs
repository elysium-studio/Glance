namespace Glance.Shell;

public sealed record ModuleInstallResult(bool IsSuccessful, IReadOnlyList<string> ComponentIds, bool RequiresRestart, string? ErrorMessage, string? PackageId, IReadOnlyList<string> QuickConverterIds, IReadOnlyList<string> InspectorProviderIds)
{
    public static ModuleInstallResult Installed(IEnumerable<string> componentIds, string? packageId = null, IEnumerable<string>? quickConverterIds = null, IEnumerable<string>? inspectorProviderIds = null) => new(true, [.. componentIds], false, null, packageId, [.. quickConverterIds ?? []], [.. inspectorProviderIds ?? []]);

    public static ModuleInstallResult Staged(IEnumerable<string> componentIds, string? packageId = null, IEnumerable<string>? quickConverterIds = null, IEnumerable<string>? inspectorProviderIds = null) => new(true, [.. componentIds], true, null, packageId, [.. quickConverterIds ?? []], [.. inspectorProviderIds ?? []]);

    public static ModuleInstallResult Failed(string message) => new(false, [], false, message, null, [], []);
}
