using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Glance.Stash.WinUI;

public sealed class StashTextViewerService
{
    private static readonly TimeSpan FileLifetime = TimeSpan.FromDays(7);
    private readonly string directory;

    public StashTextViewerService()
    {
        directory = Path.Combine(Path.GetTempPath(), "Glance", "Stash");
        _ = Directory.CreateDirectory(directory);
        DeleteExpiredFiles();
    }

    public async Task OpenAsync(string id,
        string content)
    {
        string path = Path.Combine(directory, $"{id}.txt");
        MakeWritable(path);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        _ = Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    public void Remove(string id) => TryDelete(Path.Combine(directory, $"{id}.txt"));

    private void DeleteExpiredFiles()
    {
        DateTime cutoff = DateTime.UtcNow - FileLifetime;

        foreach (string path in Directory.EnumerateFiles(directory, "*.txt"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    TryDelete(path);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            MakeWritable(path);
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    private static void MakeWritable(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
