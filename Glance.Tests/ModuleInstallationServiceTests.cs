using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class ModuleInstallationServiceTests
{
    [Fact]
    public async Task HeadlessPackageCanBeUninstalledByPackageId()
    {
        ModuleInstallationService service = new();
        bool uninstalled = false;
        service.Register("QuickConvertImage", [], () =>
        {
            uninstalled = true;
            return Task.FromResult(true);
        });

        bool result = await service.UninstallPackageAsync("QuickConvertImage");

        Assert.True(result);
        Assert.True(uninstalled);
    }
}
