namespace Glance.Shell.WinUI;

public interface IDesktopIslandPresentationController
{
    bool IsAssistantRequested { get; }

    bool IsContentRouteRequested { get; }

    bool IsModuleReorderRequested { get; }

    void Attach(IDesktopIslandPresentationHost host);

    void Detach();

    void Initialize();

    void StopAttentionExpansion();

    void TransientPresentationChanged();

    void LoadingModulesChanged();

    void SelectedComponentChanged();

    void ContentRouteVisibilityChanged();

    void ModuleReorderVisibilityChanged();
}
