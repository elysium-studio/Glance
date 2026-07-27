namespace Glance.Shell;

public sealed class ModulesDescriptionViewModel :
    IModulesViewModel
{
    public void Dispose() => GC.SuppressFinalize(this);
}
