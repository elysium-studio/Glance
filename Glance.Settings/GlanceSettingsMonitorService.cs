using Elysium.Application;
using Elysium.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Glance.Settings;

internal sealed class GlanceSettingsMonitorService<TOptions>(IWritableOptions<TOptions> options, IGlanceSettingsChangePublisher<TOptions> changePublisher, IServiceProvider provider, string filePath, ILogger<GlanceSettingsMonitorService<TOptions>> logger) :
    IHostedService,
    IDisposable
    where TOptions : class, new()
{
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource lifetime = new();
    private FileSystemWatcher? watcher;
    private Task? monitorTask;
    private bool disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (watcher is not null)
        {
            return Task.CompletedTask;
        }

        string directory = Path.GetDirectoryName(filePath)!;
        _ = Directory.CreateDirectory(directory);
        watcher = new FileSystemWatcher(directory, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Changed += HandleChanged;
        watcher.Created += HandleChanged;
        watcher.Renamed += HandleRenamed;
        watcher.EnableRaisingEvents = true;
        monitorTask = MonitorAsync(lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? task = StopMonitoring();

        if (task is null)
        {
            return;
        }

        try
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Task? task = StopMonitoring();

        try
        {
            task?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }

        lifetime.Dispose();
    }

    private void HandleChanged(object sender, FileSystemEventArgs args) => changes.Writer.TryWrite(true);

    private void HandleRenamed(object sender, RenamedEventArgs args) => changes.Writer.TryWrite(true);

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (bool signal in changes.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);

                while (changes.Reader.TryRead(out _))
                {
                }

                try
                {
                    TOptions? current = await options.ReadAsync(cancellationToken).ConfigureAwait(false);

                    if (current is null)
                    {
                        continue;
                    }

                    changePublisher.Publish(current);

                    foreach (IOptionsChangeHandler<TOptions> handler in provider.GetServices<IOptionsChangeHandler<TOptions>>())
                    {
                        handler.Handle(current, null);
                    }

                    foreach (IAsyncOptionsChangeHandler<TOptions> handler in provider.GetServices<IAsyncOptionsChangeHandler<TOptions>>())
                    {
                        await handler.HandleAsync(current, null).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to reload settings from {FilePath}", filePath);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private Task? StopMonitoring()
    {
        FileSystemWatcher? current = watcher;
        watcher = null;
        lifetime.Cancel();

        if (current is not null)
        {
            current.EnableRaisingEvents = false;
            current.Changed -= HandleChanged;
            current.Created -= HandleChanged;
            current.Renamed -= HandleRenamed;
            current.Dispose();
        }

        return monitorTask;
    }
}
