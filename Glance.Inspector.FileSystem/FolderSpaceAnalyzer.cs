namespace Glance.Inspector.FileSystem;

internal sealed class FolderSpaceAnalyzer :
    IFolderSpaceAnalyzer
{
    public Task<FolderSpaceAnalysis> AnalyzeAsync(string path, CancellationToken cancellationToken) => Task.Run(() => Analyze(path, cancellationToken), cancellationToken);

    private static FolderSpaceAnalysis Analyze(string path, CancellationToken cancellationToken)
    {
        DirectoryInfo root = new(path);
        List<FolderSpaceEntry> entries = [];
        long fileCount = 0;
        long totalBytes = 0;

        foreach (FileSystemInfo item in EnumerateChildren(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            (long size, long files) = item switch
            {
                FileInfo file => (GetLength(file), 1),
                DirectoryInfo directory => AnalyzeDirectory(directory, cancellationToken),
                _ => (0, 0)
            };
            entries.Add(new FolderSpaceEntry(item.Name, size));
            totalBytes += size;
            fileCount += files;
        }

        return new FolderSpaceAnalysis(entries.OrderByDescending(entry => entry.Size).ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), totalBytes, fileCount);
    }

    private static (long Size, long Files) AnalyzeDirectory(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        long files = 0;
        long size = 0;

        try
        {
            EnumerationOptions options = new()
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            };

            foreach (FileInfo file in directory.EnumerateFiles("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += GetLength(file);
                files++;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        return (size, files);
    }

    private static IReadOnlyList<FileSystemInfo> EnumerateChildren(DirectoryInfo directory)
    {
        try
        {
            EnumerationOptions options = new()
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };
            return directory.EnumerateFileSystemInfos("*", options).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static long GetLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }
}
