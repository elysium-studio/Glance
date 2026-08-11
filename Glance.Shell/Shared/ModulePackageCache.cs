using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Glance.Shell;

public sealed class ModulePackageCache
{
    private const string StateFileName = "state.dat";
    private readonly string cacheDirectory;

    public ModulePackageCache(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        this.cacheDirectory = Path.GetFullPath(cacheDirectory);
    }

    public string Prepare(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        string fullPackagePath = Path.GetFullPath(packagePath);
        FileInfo package = new(fullPackagePath);

        if (!package.Exists)
        {
            throw new FileNotFoundException("The module package could not be found.", fullPackagePath);
        }

        string packageCacheDirectory = cacheDirectory;
        string contentHash = HashFile(fullPackagePath);

        if (TryGetCurrentContentDirectory(packageCacheDirectory, package, contentHash, out string currentContentDirectory))
        {
            return currentContentDirectory;
        }

        string contentDirectory = Path.Combine(packageCacheDirectory, contentHash);

        Extract(fullPackagePath, packageCacheDirectory, contentDirectory);

        WriteState(packageCacheDirectory, package, contentHash);
        DeleteOldContentDirectories(packageCacheDirectory, contentDirectory);

        return contentDirectory;
    }

    private static bool TryGetCurrentContentDirectory(string packageCacheDirectory,
        FileInfo package,
        string contentHash,
        out string contentDirectory)
    {
        contentDirectory = string.Empty;
        string statePath = Path.Combine(packageCacheDirectory, StateFileName);

        if (!File.Exists(statePath))
        {
            return false;
        }

        string[] values = File.ReadAllText(statePath).Split('|');

        if (values.Length != 3 ||
            !long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out long length) ||
            !long.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out long lastWriteTimeUtcTicks) ||
            length != package.Length ||
            lastWriteTimeUtcTicks != package.LastWriteTimeUtc.Ticks ||
            !string.Equals(values[2], contentHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string candidate = Path.Combine(packageCacheDirectory, values[2]);

        if (!Directory.Exists(candidate) ||
            !IsExtractedPackageComplete(package.FullName, candidate))
        {
            return false;
        }

        contentDirectory = candidate;

        return true;
    }

    private static void Extract(string packagePath, string packageCacheDirectory, string contentDirectory)
    {
        _ = Directory.CreateDirectory(packageCacheDirectory);

        string temporaryDirectory = Path.Combine(packageCacheDirectory, $".extract-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(temporaryDirectory);

        try
        {
            string temporaryDirectoryPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(temporaryDirectory)) + Path.DirectorySeparatorChar;

            using ZipArchive archive = ZipFile.OpenRead(packagePath);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(temporaryDirectory, entry.FullName));

                if (!destinationPath.StartsWith(temporaryDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The module package entry '{entry.FullName}' resolves outside the extraction directory.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    _ = Directory.CreateDirectory(destinationPath);
                    continue;
                }

                _ = Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath);
            }

            if (!Directory.Exists(contentDirectory))
            {
                Directory.Move(temporaryDirectory, contentDirectory);
                return;
            }

            foreach (string extractedFile in Directory.EnumerateFiles(temporaryDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(temporaryDirectory, extractedFile);
                string destinationPath = Path.Combine(contentDirectory, relativePath);

                if (!File.Exists(destinationPath))
                {
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Move(extractedFile, destinationPath);
                }
            }

            Directory.Delete(temporaryDirectory, true);
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            throw;
        }
    }

    private static bool IsExtractedPackageComplete(string packagePath,
        string contentDirectory)
    {
        string contentDirectoryPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentDirectory)) + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(packagePath);

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            string destinationPath = Path.GetFullPath(Path.Combine(contentDirectory, entry.FullName));

            if (!destinationPath.StartsWith(contentDirectoryPrefix, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(destinationPath))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteState(string packageCacheDirectory, FileInfo package, string contentHash)
    {
        string statePath = Path.Combine(packageCacheDirectory, StateFileName);
        string temporaryStatePath = $"{statePath}.{Guid.NewGuid():N}.tmp";
        string state = string.Join('|', package.Length.ToString(CultureInfo.InvariantCulture), package.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture), contentHash);

        File.WriteAllText(temporaryStatePath, state, Encoding.UTF8);
        File.Move(temporaryStatePath, statePath, true);
    }

    private static void DeleteOldContentDirectories(string packageCacheDirectory, string currentContentDirectory)
    {
        foreach (string directory in Directory.EnumerateDirectories(packageCacheDirectory))
        {
            if (string.Equals(directory, currentContentDirectory, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(directory).StartsWith(".extract-", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static string HashFile(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

}
