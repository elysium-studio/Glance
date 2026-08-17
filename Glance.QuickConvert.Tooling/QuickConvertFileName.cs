namespace Glance.QuickConvert.Tooling;

public static class QuickConvertFileName
{
    public static string Create(string sourcePath,
        string extension,
        Func<string, bool> exists)
    {
        string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
        string candidate = Path.Combine(directory, $"{name}.{normalizedExtension}");

        if (!exists(candidate) && !string.Equals(candidate, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        candidate = Path.Combine(directory, $"{name} converted.{normalizedExtension}");

        for (int suffix = 2; exists(candidate); suffix++)
        {
            candidate = Path.Combine(directory, $"{name} converted {suffix}.{normalizedExtension}");
        }

        return candidate;
    }
}
