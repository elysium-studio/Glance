using System;
using System.IO;

namespace Glance.UI.WinUI;

public static class GlanceModuleData
{
    private const string ApplicationDirectoryName = "Glance";
    private const string ModulesDirectoryName = "Modules";

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDirectoryName,
        ModulesDirectoryName,
        "Data");

    public static string GetDirectory(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (moduleId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            moduleId.Contains(Path.DirectorySeparatorChar) ||
            moduleId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("A module identifier must be a valid directory name.", nameof(moduleId));
        }

        return Path.Combine(RootDirectory, moduleId);
    }

    public static string GetPath(string moduleId, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(GetDirectory(moduleId), fileName);
    }
}
