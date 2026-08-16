using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Glance.FoundryLocal;

public static partial class FoundryLocalRuntime
{
    private static readonly SemaphoreSlim initializationGate = new(1, 1);
    private static readonly object nativeLibraryGate = new();
    private static readonly List<nint> nativeLibraryHandles = [];
    private static bool nativeLibrariesLoaded;

    public static async Task EnsureInitializedAsync(ILogger logger,
        CancellationToken cancellationToken = default)
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
                EnsureNativeLibrariesLoaded();
                await FoundryLocalManager.CreateAsync(new Configuration { AppName = "Glance" }, logger, cancellationToken);
            }
        }
        finally
        {
            _ = initializationGate.Release();
        }
    }

    private static void EnsureNativeLibrariesLoaded()
    {
        lock (nativeLibraryGate)
        {
            if (nativeLibrariesLoaded)
            {
                return;
            }

            string directory = Path.GetDirectoryName(typeof(FoundryLocalRuntime).Assembly.Location) ?? AppContext.BaseDirectory;
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            if (!currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", $"{directory}{Path.PathSeparator}{currentPath}");
            }

            foreach (string fileName in (string[])["onnxruntime_providers_shared.dll", "onnxruntime.dll", "onnxruntime-genai.dll"])
            {
                string path = Path.Combine(directory, fileName);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"The Foundry Local runtime dependency {fileName} was not found", path);
                }

                nint handle = LoadLibraryEx(path, 0, 0x00000100 | 0x00001000);

                if (handle == 0)
                {
                    throw new DllNotFoundException($"Unable to load {fileName}. Windows error {Marshal.GetLastWin32Error()}");
                }

                nativeLibraryHandles.Add(handle);
            }

            nativeLibrariesLoaded = true;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryEx(string fileName, nint file, uint flags);
}
