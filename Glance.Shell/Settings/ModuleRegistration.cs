namespace Glance.Shell;

internal sealed record ModuleRegistration(string PackageId, string Version, Func<Task<bool>> Uninstall);
