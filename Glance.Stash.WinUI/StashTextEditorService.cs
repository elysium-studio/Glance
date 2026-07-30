using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Glance.Stash.WinUI;

public sealed class StashTextEditorService :
    IDisposable
{
    private static readonly TimeSpan FileLifetime = TimeSpan.FromDays(7);
    private readonly ConcurrentDictionary<string, EditorSession> sessions = [];
    private readonly string directory;

    public StashTextEditorService()
    {
        directory = Path.Combine(Path.GetTempPath(), "Glance", "Stash");
        Directory.CreateDirectory(directory);
        DeleteExpiredFiles();
    }

    public async Task OpenAsync(string id,
        string content,
        Func<string, Task> contentChanged)
    {
        string path = Path.Combine(directory, $"{id}.txt");

        if (!sessions.TryGetValue(id, out EditorSession? session))
        {
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
            EditorSession candidate = new(path, content, contentChanged);
            session = sessions.GetOrAdd(id, candidate);

            if (!ReferenceEquals(session, candidate))
            {
                candidate.Dispose();
            }
        }

        Process.Start(new ProcessStartInfo(session.Path)
        {
            UseShellExecute = true
        });
    }

    public void Remove(string id)
    {
        if (sessions.TryRemove(id, out EditorSession? session))
        {
            session.Dispose();
        }

        TryDelete(Path.Combine(directory, $"{id}.txt"));
    }

    public void Dispose()
    {
        foreach (EditorSession session in sessions.Values)
        {
            session.Dispose();
        }

        sessions.Clear();
    }

    private void DeleteExpiredFiles()
    {
        DateTime cutoff = DateTime.UtcNow - FileLifetime;

        foreach (string path in Directory.EnumerateFiles(directory, "*.txt"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
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
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    private sealed class EditorSession :
        IDisposable
    {
        private readonly Func<string, Task> contentChanged;
        private readonly SemaphoreSlim refreshLock = new(1, 1);
        private readonly FileSystemWatcher watcher;
        private CancellationTokenSource? refreshCancellation;
        private string content;
        private bool disposed;

        public EditorSession(string path,
            string content,
            Func<string, Task> contentChanged)
        {
            Path = path;
            this.content = content;
            this.contentChanged = contentChanged;
            watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed += HandleFileChanged;
            watcher.Created += HandleFileChanged;
            watcher.Renamed += HandleFileRenamed;
            watcher.EnableRaisingEvents = true;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleFileChanged;
            watcher.Created -= HandleFileChanged;
            watcher.Renamed -= HandleFileRenamed;
            watcher.Dispose();
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshLock.Dispose();
        }

        private void HandleFileChanged(object sender,
            FileSystemEventArgs args) =>
            ScheduleRefresh();

        private void HandleFileRenamed(object sender,
            RenamedEventArgs args) =>
            ScheduleRefresh();

        private void ScheduleRefresh()
        {
            if (disposed)
            {
                return;
            }

            CancellationTokenSource cancellation = new();
            CancellationTokenSource? previous = Interlocked.Exchange(ref refreshCancellation, cancellation);
            previous?.Cancel();
            previous?.Dispose();
            _ = RefreshAsync(cancellation.Token);
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken);
                await refreshLock.WaitAsync(cancellationToken);

                try
                {
                    string updatedContent = await ReadAsync(cancellationToken);

                    if (string.Equals(content, updatedContent, StringComparison.Ordinal))
                    {
                        return;
                    }

                    content = updatedContent;
                    await contentChanged(updatedContent);
                }
                finally
                {
                    refreshLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        private async Task<string> ReadAsync(CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await using FileStream stream = new(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
                    using StreamReader reader = new(stream, Encoding.UTF8, true);
                    return await reader.ReadToEndAsync(cancellationToken);
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            return content;
        }
    }
}
