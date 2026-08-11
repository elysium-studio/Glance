using System;
using System.Collections.Generic;
using System.IO;

namespace Glance.Shell.WinUI;

internal static class GlanceModuleDataMigration
{
    private static readonly IReadOnlyDictionary<string, string> LegacySettingOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["clipboard.settings.dat"] = "Clipboard",
        ["color-picker.settings.dat"] = "ColorPicker",
        ["device-presence.settings.dat"] = "DevicePresence",
        ["drop-shelf.settings.dat"] = "DropShelf",
        ["focus-session.settings.dat"] = "FocusSession",
        ["keep-awake.settings.dat"] = "KeepAwake",
        ["media.settings.dat"] = "Media",
        ["power.settings.dat"] = "Power",
        ["presence.settings.dat"] = "Presence",
        ["screen-capture.settings.dat"] = "ScreenCapture",
        ["screen-recorder.settings.dat"] = "ScreenRecorder",
        ["stopwatch.settings.dat"] = "Stopwatch",
        ["system-monitor.settings.dat"] = "SystemMonitor",
        ["theme-switcher.settings.dat"] = "ThemeSwitcher",
        ["timer.settings.dat"] = "Timer",
        ["voice-notes.settings.dat"] = "VoiceNotes",
        ["weather.settings.dat"] = "Weather",
        ["world-clock.settings.dat"] = "WorldClock"
    };

    private static readonly string[] LegacyDataDirectories =
    [
        "Clipboard",
        "ColorPicker",
        "Reminders",
        "ScreenCapture",
        "Stash",
        "VoiceNotes"
    ];

    public static void Migrate(string applicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationData);
        string modulesDirectory = Path.Combine(applicationData, "Modules");
        _ = Directory.CreateDirectory(modulesDirectory);

        foreach ((string fileName, string moduleId) in LegacySettingOwners)
        {
            MoveFileIfNeeded(Path.Combine(applicationData, fileName), Path.Combine(modulesDirectory, moduleId, fileName));
        }

        foreach (string moduleId in LegacyDataDirectories)
        {
            MergeDirectory(Path.Combine(applicationData, moduleId), Path.Combine(modulesDirectory, moduleId));
        }

        string legacyCache = Path.Combine(applicationData, "ModuleCache");

        if (Directory.Exists(legacyCache))
        {
            try
            {
                Directory.Delete(legacyCache, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void MergeDirectory(string sourceDirectory,
        string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (string sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            MoveFileIfNeeded(sourcePath, Path.Combine(destinationDirectory, relativePath));
        }

        try
        {
            Directory.Delete(sourceDirectory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void MoveFileIfNeeded(string sourcePath,
        string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (File.Exists(destinationPath))
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Move(sourcePath, destinationPath);
    }
}
