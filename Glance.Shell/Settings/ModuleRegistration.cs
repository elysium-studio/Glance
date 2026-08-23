namespace Glance.Shell;

internal sealed record ModuleRegistration(string PackageId, Func<Task<bool>> Uninstall);
