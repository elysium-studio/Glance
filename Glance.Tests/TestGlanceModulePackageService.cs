using Glance.Shell;

namespace Glance.Tests;

internal sealed class TestGlanceModulePackageService :
    IGlanceModulePackageService
{
    public Task<ModuleInstallResult> InstallAsync(GlanceModuleFeedItem module, CancellationToken cancellationToken = default) => Task.FromResult(ModuleInstallResult.Failed("Unavailable"));

    public bool Cancel(string moduleId) => false;
}
