using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace Glance.Archive.WinUI;

internal sealed class ArchiveService :
    IArchiveService
{
    public async Task<string> CreateAsync(IReadOnlyList<ArchiveItem> items, ArchiveOperationOptions options, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken)
    {
        string outputPath = ArchivePath.CreateArchivePath(items.Select(item => item.Path).ToArray(), options.Format);
        IReadOnlyList<ArchiveSourceFile> files = CollectFiles(items, outputPath);

        await WriteAsync(outputPath, files, options, progress, cancellationToken);
        return outputPath;
    }

    public async Task<string> ExtractAsync(string sourcePath, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken)
    {
        string outputPath = ArchivePath.CreateExtractionPath(sourcePath);
        Directory.CreateDirectory(outputPath);

        try
        {
            await ExtractIntoAsync(sourcePath, outputPath, progress, cancellationToken);
            return outputPath;
        }
        catch
        {
            DeleteDirectory(outputPath);
            throw;
        }
    }

    public async Task<string> ConvertAsync(string sourcePath, ArchiveOperationOptions options, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken)
    {
        string temporaryPath = Path.Combine(Path.GetTempPath(), "Glance", "Archive", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryPath);

        try
        {
            progress?.Report(new ArchiveOperationProgress(0));
            await ExtractIntoAsync(sourcePath, temporaryPath, null, cancellationToken);
            ArchiveItem item = new(temporaryPath, Path.GetFileNameWithoutExtension(sourcePath), true);
            string outputPath = ArchivePath.CreateArchivePath([sourcePath], options.Format);
            IReadOnlyList<ArchiveSourceFile> files = CollectFiles([item], outputPath, false);
            await WriteAsync(outputPath, files, options, progress, cancellationToken);
            return outputPath;
        }
        finally
        {
            DeleteDirectory(temporaryPath);
        }
    }

    private static async Task ExtractIntoAsync(string sourcePath, string outputPath, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(sourcePath);

        if (Path.GetExtension(sourcePath).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using IArchive archive = ArchiveFactory.OpenArchive(stream);
            IArchiveEntry[] entries = archive.Entries.ToArray();

            for (int index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IArchiveEntry entry = entries[index];
                await ExtractEntryAsync(entry.Key ?? Path.GetFileNameWithoutExtension(sourcePath), entry.IsDirectory, () => entry.OpenEntryStream(), outputPath, cancellationToken);
                Report(progress, index + 1, entries.Length);
            }

            return;
        }

        using IReader reader = ReaderFactory.OpenReader(stream);
        int completed = 0;

        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExtractEntryAsync(reader.Entry.Key ?? Path.GetFileNameWithoutExtension(sourcePath), reader.Entry.IsDirectory, reader.OpenEntryStream, outputPath, cancellationToken);
            Report(progress, ++completed, Math.Max(completed + 1, 1));
        }

        progress?.Report(new ArchiveOperationProgress(1, true));
    }

    private static async Task ExtractEntryAsync(string key, bool isDirectory, Func<Stream> openStream, string outputPath, CancellationToken cancellationToken)
    {
        string entryPath = ArchivePath.GetSafeEntryPath(outputPath, key);

        if (isDirectory)
        {
            Directory.CreateDirectory(entryPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
        await using Stream source = openStream();
        await using FileStream destination = new(entryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static async Task WriteAsync(string outputPath, IReadOnlyList<ArchiveSourceFile> files, ArchiveOperationOptions options, IProgress<ArchiveOperationProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream output = new(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true);
            ArchiveType archiveType = options.Format switch
            {
                ArchiveFormat.SevenZip => ArchiveType.SevenZip,
                ArchiveFormat.Tar or ArchiveFormat.TarGZip => ArchiveType.Tar,
                _ => ArchiveType.Zip
            };
            CompressionType compressionType = options.Format switch
            {
                ArchiveFormat.SevenZip => CompressionType.LZMA2,
                ArchiveFormat.TarGZip => CompressionType.GZip,
                ArchiveFormat.Tar => CompressionType.None,
                _ => CompressionType.Deflate
            };
            WriterOptions writerOptions = new(compressionType) { LeaveStreamOpen = true };

            if (compressionType is CompressionType.Deflate or CompressionType.GZip)
            {
                writerOptions.CompressionLevel = options.CompressionLevel switch
                {
                    ArchiveCompressionLevel.Fast => 1,
                    ArchiveCompressionLevel.Smallest => 9,
                    _ => 5
                };
            }
            using IWriter writer = WriterFactory.OpenWriter(output, archiveType, writerOptions);

            for (int index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchiveSourceFile file = files[index];

                if (file.IsDirectory)
                {
                    writer.WriteDirectory(file.EntryName, null);
                }
                else
                {
                    await using FileStream source = new(file.Path!, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                    writer.Write(file.EntryName, source, File.GetLastWriteTime(file.Path!));
                }

                Report(progress, index + 1, files.Count);
            }

            progress?.Report(new ArchiveOperationProgress(1, true));
        }
        catch
        {
            File.Delete(outputPath);
            throw;
        }
    }

    private static IReadOnlyList<ArchiveSourceFile> CollectFiles(IReadOnlyList<ArchiveItem> items, string outputPath, bool includeFolderName = true)
    {
        List<ArchiveSourceFile> files = [];
        HashSet<string> entryNames = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (ArchiveItem item in items)
        {
            if (!item.IsFolder)
            {
                if (!Path.GetFullPath(item.Path).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                {
                    string entryName = CreateUniqueEntryName(Path.GetFileName(item.Path), entryNames);
                    files.Add(new ArchiveSourceFile(item.Path, entryName, false));
                }

                continue;
            }

            string rootName = includeFolderName ? CreateUniqueEntryName(new DirectoryInfo(item.Path).Name, entryNames) : string.Empty;

            if (!string.IsNullOrEmpty(rootName))
            {
                files.Add(new ArchiveSourceFile(null, rootName, true));
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(item.Path, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(item.Path, directoryPath).Replace(Path.DirectorySeparatorChar, '/');
                string entryName = string.IsNullOrEmpty(rootName) ? relativePath : $"{rootName}/{relativePath}";
                _ = entryNames.Add(entryName);
                files.Add(new ArchiveSourceFile(null, entryName, true));
            }

            foreach (string filePath in Directory.EnumerateFiles(item.Path, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(item.Path, filePath).Replace(Path.DirectorySeparatorChar, '/');
                string requestedEntryName = string.IsNullOrEmpty(rootName) ? relativePath : $"{rootName}/{relativePath}";
                string entryName = CreateUniqueEntryName(requestedEntryName, entryNames);
                files.Add(new ArchiveSourceFile(filePath, entryName, false));
            }
        }

        return files;
    }

    private static string CreateUniqueEntryName(string entryName, HashSet<string> entryNames)
    {
        if (entryNames.Add(entryName))
        {
            return entryName;
        }

        string directory = Path.GetDirectoryName(entryName)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(entryName);
        string extension = Path.GetExtension(entryName);
        int suffix = 1;
        string candidate;

        do
        {
            string fileName = $"{name} ({suffix++}){extension}";
            candidate = string.IsNullOrEmpty(directory) ? fileName : $"{directory}/{fileName}";
        }
        while (!entryNames.Add(candidate));

        return candidate;
    }

    private static void Report(IProgress<ArchiveOperationProgress>? progress, int completed, int total) => progress?.Report(new ArchiveOperationProgress(total == 0 ? 1 : (double)completed / total));

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
