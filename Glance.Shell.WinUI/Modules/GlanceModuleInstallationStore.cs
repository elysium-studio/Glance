using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Glance.Shell.WinUI;

internal static class GlanceModuleInstallationStore
{
    private const string ModulesDirectoryName = "Modules";
    private const string RemovedDirectoryPrefix = ".removed-";

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Glance",
        ModulesDirectoryName,
        "Packages");

    public static void PrepareForStartup()
    {
        _ = Directory.CreateDirectory(RootDirectory);
        DeletePendingDirectories();
    }

    public static void RemoveSuppressedPackages(IEnumerable<string> packageIds)
    {
        foreach (string packageId in packageIds.Where(packageId => !string.IsNullOrWhiteSpace(packageId)))
        {
            DeleteOrQuarantine(GetModuleDirectory(packageId));
        }
    }

    public static string GetPackageId(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        string? directory = Path.GetDirectoryName(fullPackagePath);

        if (directory is not null &&
            string.Equals(Path.GetDirectoryName(directory), RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(directory);
        }

        return Path.GetFileNameWithoutExtension(fullPackagePath);
    }

    public static string StagePackage(string packagePath)
    {
        string sourcePath = Path.GetFullPath(packagePath);
        ValidatePackage(sourcePath);
        string packageId = Path.GetFileNameWithoutExtension(sourcePath);
        string moduleDirectory = GetModuleDirectory(packageId);
        string destinationPath = Path.Combine(moduleDirectory, $"{packageId}.glance");
        string temporaryPath = Path.Combine(moduleDirectory, $".{packageId}.{Guid.NewGuid():N}.installing");
        _ = Directory.CreateDirectory(moduleDirectory);

        try
        {
            File.Copy(sourcePath, temporaryPath, true);
            File.Move(temporaryPath, destinationPath, true);
            return destinationPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void RemovePackageForCurrentProcess(string packagePath)
    {
        string moduleDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath))!;

        if (!Directory.Exists(moduleDirectory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(moduleDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteFile(file);
        }

        foreach (string directory in Directory.EnumerateDirectories(moduleDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            if (!string.Equals(Path.GetFileName(directory), "Runtime", StringComparison.OrdinalIgnoreCase))
            {
                _ = TryDeleteDirectory(directory);
            }
        }
    }

    public static void DeletePackagePayload(string packagePath)
    {
        string fullPackagePath = Path.GetFullPath(packagePath);
        string moduleDirectory = Path.GetDirectoryName(fullPackagePath)!;
        TryDeleteFile(fullPackagePath);
        _ = TryDeleteDirectory(Path.Combine(moduleDirectory, "Runtime"));

        if (Directory.Exists(moduleDirectory) && !Directory.EnumerateFileSystemEntries(moduleDirectory).Any())
        {
            _ = TryDeleteDirectory(moduleDirectory);
        }
    }

    private static string GetModuleDirectory(string packageId) => Path.Combine(RootDirectory, packageId);

    private static void ValidatePackage(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        HashSet<string> entries = [with(StringComparer.OrdinalIgnoreCase), .. archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .Select(entry => entry.FullName)];
        bool containsResourceModule = entries
            .Where(entry => string.Equals(Path.GetExtension(entry), ".dll", StringComparison.OrdinalIgnoreCase))
            .Any(assembly => entries.Contains(Path.ChangeExtension(assembly, ".pri")));
        bool containsHeadlessModule = entries
            .Where(entry => entry.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(Path.GetDirectoryName(entry)))
            .Select(entry => entry[..^".deps.json".Length] + ".dll")
            .Any(entries.Contains);

        if (!containsResourceModule && !containsHeadlessModule)
        {
            throw new InvalidDataException("The package does not contain a Glance module entry assembly.");
        }
    }

    private static void DeletePendingDirectories()
    {
        foreach (string directory in Directory.EnumerateDirectories(RootDirectory, $"{RemovedDirectoryPrefix}*", SearchOption.TopDirectoryOnly))
        {
            TryDeleteDirectory(directory);
        }
    }

    private static void DeleteOrQuarantine(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        if (TryDeleteDirectory(directory))
        {
            return;
        }

        string quarantineDirectory = Path.Combine(RootDirectory, $"{RemovedDirectoryPrefix}{Guid.NewGuid():N}");

        try
        {
            Directory.Move(directory, quarantineDirectory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
