namespace Glance.Archive;

public interface IArchiveService
{
    Task<string> CreateAsync(IReadOnlyList<ArchiveItem> items, ArchiveOperationOptions options, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken);

    Task<string> ExtractAsync(string sourcePath, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken);

    Task<string> ConvertAsync(string sourcePath, ArchiveOperationOptions options, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken);
}
