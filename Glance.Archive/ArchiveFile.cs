namespace Glance.Archive;

public static class ArchiveFile
{
    public static readonly string[] SupportedExtensions =
    [
        ".tar.bz2", ".tar.gz", ".tar.xz", ".tbz2", ".tgz", ".txz", ".7z", ".bz2", ".gz", ".rar", ".tar", ".xz", ".zip"
    ];

    public static bool IsArchive(string path) => SupportedExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
