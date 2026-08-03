using System.Diagnostics;

namespace Glance.Assistant;

internal static class AssistantWakeDiagnostics
{
    private static readonly Lock synchronization = new();
    private static bool initialized;

    public static string LogPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Logs", "assistant-wake.log");

    public static void Write(string eventName, string details)
    {
        try
        {
            lock (synchronization)
            {
                Initialize();
                string timestamp = DateTimeOffset.Now.ToString("O");
                string safeDetails = details.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                File.AppendAllText(LogPath, $"{timestamp}\tPID={Environment.ProcessId}\tTID={Environment.CurrentManagedThreadId}\t{eventName}\t{safeDetails}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
        }
    }

    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(LogPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 5 * 1024 * 1024)
        {
            File.Copy(LogPath, Path.ChangeExtension(LogPath, ".previous.log"), true);
            File.WriteAllText(LogPath, string.Empty);
        }

        initialized = true;
        File.AppendAllText(LogPath, $"{Environment.NewLine}{DateTimeOffset.Now:O}\tPID={Environment.ProcessId}\tSESSION\tGlance wake diagnostics started; Version={typeof(AssistantWakeDiagnostics).Assembly.GetName().Version}; Process={Process.GetCurrentProcess().ProcessName}{Environment.NewLine}");
    }
}
