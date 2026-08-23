using Glance.Application.Abstractions;
using System.Net.Http;

namespace Glance.Shell;

public sealed class BackgroundDownloadManager(HttpClient httpClient) :
    IBackgroundDownloadManager,
    IDisposable
{
    private const int BufferSize = 1024 * 1024;
    private readonly Dictionary<string, DownloadOperation> operations = new(StringComparer.OrdinalIgnoreCase);
    private readonly object sync = new();
    private bool disposed;

    public event EventHandler<BackgroundDownloadChangedEventArgs>? DownloadChanged;

    public IReadOnlyList<BackgroundDownloadSnapshot> Downloads
    {
        get
        {
            lock (sync)
            {
                return [.. operations.Values.Select(operation => operation.Snapshot)];
            }
        }
    }

    public BackgroundDownloadSnapshot Enqueue(BackgroundDownloadRequest request)
    {
        DownloadOperation operation;

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (operations.TryGetValue(request.Id, out DownloadOperation? existing) &&
                existing.Snapshot.IsActive)
            {
                return existing.Snapshot;
            }

            operation = new DownloadOperation(request);
            operations[request.Id] = operation;
            operation.Completion = DownloadAsync(operation);
        }

        Publish(operation.Snapshot);
        return operation.Snapshot;
    }

    public BackgroundDownloadSnapshot? GetDownload(string id)
    {
        lock (sync)
        {
            return operations.TryGetValue(id, out DownloadOperation? operation)
                ? operation.Snapshot
                : null;
        }
    }

    public async Task<BackgroundDownloadSnapshot> WaitForCompletionAsync(string id,
        CancellationToken cancellationToken = default)
    {
        Task<BackgroundDownloadSnapshot> completion;

        lock (sync)
        {
            if (!operations.TryGetValue(id, out DownloadOperation? operation) ||
                operation.Completion is null)
            {
                throw new InvalidOperationException($"Background download '{id}' was not found.");
            }

            completion = operation.Completion;
        }

        return await completion.WaitAsync(cancellationToken);
    }

    public bool Cancel(string id)
    {
        lock (sync)
        {
            if (!operations.TryGetValue(id, out DownloadOperation? operation) ||
                !operation.Snapshot.IsActive)
            {
                return false;
            }

            operation.Cancellation.Cancel();
            return true;
        }
    }

    public bool Remove(string id)
    {
        DownloadOperation operation;

        lock (sync)
        {
            if (!operations.TryGetValue(id, out DownloadOperation? selected) ||
                selected.Snapshot.IsActive)
            {
                return false;
            }

            operation = selected;
            _ = operations.Remove(id);
        }

        operation.Dispose();
        return true;
    }

    public void Dispose()
    {
        DownloadOperation[] downloads;

        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            downloads = [.. operations.Values];
            operations.Clear();
        }

        foreach (DownloadOperation operation in downloads)
        {
            operation.Cancellation.Cancel();

            if (operation.Completion is Task<BackgroundDownloadSnapshot> completion)
            {
                _ = completion.ContinueWith(_ => operation.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            else
            {
                operation.Dispose();
            }
        }
    }

    private async Task<BackgroundDownloadSnapshot> DownloadAsync(DownloadOperation operation)
    {
        BackgroundDownloadRequest request = operation.Request;
        string temporaryPath = request.TemporaryPath ?? $"{request.DestinationPath}.download";

        try
        {
            string? directory = Path.GetDirectoryName(request.DestinationPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            using HttpResponseMessage response = await httpClient.GetAsync(request.Source,
                HttpCompletionOption.ResponseHeadersRead,
                operation.Cancellation.Token);
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;
            Update(operation, BackgroundDownloadStatus.Downloading, 0, totalBytes, null);
            long bytesReceived = 0;

            {
                await using Stream source = await response.Content.ReadAsStreamAsync(operation.Cancellation.Token);
                await using FileStream destination = new(temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                byte[] buffer = new byte[BufferSize];

                while (true)
                {
                    int read = await source.ReadAsync(buffer, operation.Cancellation.Token);

                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), operation.Cancellation.Token);
                    bytesReceived += read;
                    Update(operation, BackgroundDownloadStatus.Downloading, bytesReceived, totalBytes, null);
                }

                await destination.FlushAsync(operation.Cancellation.Token);
            }

            File.Move(temporaryPath, request.DestinationPath, true);
            return Update(operation,
                BackgroundDownloadStatus.Completed,
                bytesReceived,
                totalBytes ?? bytesReceived,
                null);
        }
        catch (OperationCanceledException)
        {
            File.Delete(temporaryPath);
            return Update(operation,
                BackgroundDownloadStatus.Cancelled,
                operation.Snapshot.BytesReceived,
                operation.Snapshot.TotalBytes,
                null);
        }
        catch (Exception exception)
        {
            File.Delete(temporaryPath);
            return Update(operation,
                BackgroundDownloadStatus.Failed,
                operation.Snapshot.BytesReceived,
                operation.Snapshot.TotalBytes,
                exception.Message);
        }
    }

    private BackgroundDownloadSnapshot Update(DownloadOperation operation,
        BackgroundDownloadStatus status,
        long bytesReceived,
        long? totalBytes,
        string? errorMessage)
    {
        BackgroundDownloadSnapshot snapshot;

        lock (sync)
        {
            snapshot = operation.Snapshot with
            {
                Status = status,
                BytesReceived = bytesReceived,
                TotalBytes = totalBytes,
                ErrorMessage = errorMessage
            };
            operation.Snapshot = snapshot;
        }

        Publish(snapshot);
        return snapshot;
    }

    private void Publish(BackgroundDownloadSnapshot snapshot)
    {
        EventHandler<BackgroundDownloadChangedEventArgs>? handlers = DownloadChanged;

        if (handlers is null)
        {
            return;
        }

        BackgroundDownloadChangedEventArgs args = new(snapshot);

        foreach (EventHandler<BackgroundDownloadChangedEventArgs> handler in handlers.GetInvocationList().Cast<EventHandler<BackgroundDownloadChangedEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch
            {
            }
        }
    }

    private sealed class DownloadOperation(BackgroundDownloadRequest request) :
        IDisposable
    {
        public BackgroundDownloadRequest Request { get; } = request;

        public CancellationTokenSource Cancellation { get; } = new();

        public BackgroundDownloadSnapshot Snapshot { get; set; } = new(request.Id,
            request.Source,
            request.DestinationPath,
            BackgroundDownloadStatus.Queued,
            0,
            null,
            null);

        public Task<BackgroundDownloadSnapshot>? Completion { get; set; }

        public void Dispose() => Cancellation.Dispose();
    }
}
