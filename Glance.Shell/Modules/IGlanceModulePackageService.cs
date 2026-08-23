namespace Glance.Shell;

public interface IGlanceModulePackageService
{
    Task<ModuleInstallResult> InstallAsync(GlanceModuleFeedItem module, CancellationToken cancellationToken = default);

    bool Cancel(string moduleId);
}
