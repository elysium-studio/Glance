using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

namespace Glance.Assistant;

internal static class FoundryLocalRuntime
{
    private static readonly SemaphoreSlim initializationGate = new(1, 1);

    public static async Task EnsureInitializedAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        if (FoundryLocalManager.IsInitialized)
        {
            return;
        }

        await initializationGate.WaitAsync(cancellationToken);

        try
        {
            if (!FoundryLocalManager.IsInitialized)
            {
                MicrosoftOfflineAssistantProvider.EnsureNativeLibrariesLoaded();
                await FoundryLocalManager.CreateAsync(new Configuration { AppName = "Glance" }, logger, cancellationToken);
            }
        }
        finally
        {
            _ = initializationGate.Release();
        }
    }
}
