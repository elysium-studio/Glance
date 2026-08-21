namespace Glance.Archive.WinUI;

internal static class ArchivePath
{
    public static string CreateArchivePath(IReadOnlyList<string> paths, ArchiveFormat format)
    {
        string sourcePath = paths[0];
        string directory = Directory.Exists(sourcePath) ? Directory.GetParent(sourcePath)!.FullName : Path.GetDirectoryName(sourcePath)!;
        string name = paths.Count == 1 ? GetName(sourcePath) : "Archive";
        string extension = GetExtension(format);
        string outputPath = Path.Combine(directory, $"{name}{extension}");
        int suffix = 1;

        while (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            outputPath = Path.Combine(directory, $"{name} ({suffix++}){extension}");
        }

        return outputPath;
    }

    public static string CreateExtractionPath(string sourcePath)
    {
        string directory = Path.GetDirectoryName(sourcePath)!;
        string name = GetName(sourcePath);
        string outputPath = Path.Combine(directory, name);
        int suffix = 1;

        while (File.Exists(outputPath) || Directory.Exists(outputPath))
        {
            outputPath = Path.Combine(directory, $"{name} ({suffix++})");
        }

        return outputPath;
    }

    public static string GetSafeEntryPath(string destinationPath, string entryKey)
    {
        string normalizedPath = entryKey.Replace('/', Path.DirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(normalizedPath) || Path.IsPathRooted(normalizedPath))
        {
            throw new InvalidDataException("The archive contains an invalid file path.");
        }

        string relativePath = normalizedPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootPath = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string outputPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));

        if (!outputPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The archive contains an unsafe file path.");
        }

        return outputPath;
    }

    public static string GetExtension(ArchiveFormat format) => format switch
    {
        ArchiveFormat.SevenZip => ".7z",
        ArchiveFormat.Tar => ".tar",
        ArchiveFormat.TarGZip => ".tar.gz",
        _ => ".zip"
    };

    private static string GetName(string path)
    {
        string name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileName(path);

        foreach (string extension in ArchiveFile.SupportedExtensions.OrderByDescending(extension => extension.Length))
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^extension.Length];
            }
        }

        return Path.GetFileNameWithoutExtension(name);
    }
}
