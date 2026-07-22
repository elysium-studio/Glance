using System;
using System.IO;
using Velopack;

namespace Glance.Shell.WinUI;

public static class UninstallCleanup
{
    public static void Run(SemanticVersion version)
    {
        try
        {
            string applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance");

            if (Directory.Exists(applicationDataPath))
            {
                Directory.Delete(applicationDataPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}
