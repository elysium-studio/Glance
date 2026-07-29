using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Glance.Media.WinUI;

internal static class MediaTransitionDiagnostics
{
    private static readonly object sync = new();
    private static readonly string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "media-transition-diagnostics.log");
    private static bool initialized;

    public static string FilePath => filePath;

    public static string Identify(object? value) => value is null ? "null" : $"{value.GetType().Name}#{RuntimeHelpers.GetHashCode(value):X8}";

    public static void Write(string source, string message)
    {
        try
        {
            lock (sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                if (!initialized)
                {
                    File.WriteAllText(filePath, $"Media transition diagnostics started {DateTimeOffset.Now:O}{Environment.NewLine}");
                    initialized = true;
                }

                File.AppendAllText(filePath, $"{DateTimeOffset.Now:O} | Tick={Stopwatch.GetTimestamp()} | Thread={Environment.CurrentManagedThreadId} | {source} | {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}
