namespace Glance.Shell;

public sealed class GlanceModuleFeedValidator :
    IGlanceModuleFeedValidator
{
    public void Validate(GlanceModuleFeed feed, GlanceModuleFeedSource source)
    {
        ArgumentNullException.ThrowIfNull(feed);

        bool validSource = source.Uri.Scheme == Uri.UriSchemeHttps || source.AllowLocalPackages && source.Uri.IsFile;

        if (!validSource || feed.SchemaVersion != 1 || !string.Equals(feed.Channel, "stable", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The module catalogue format is not supported.");
        }

        HashSet<string> ids = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (GlanceModuleFeedItem module in feed.Modules)
        {
            bool validPackageLocation = module.DownloadUrl.Scheme == Uri.UriSchemeHttps || source.AllowLocalPackages && IsLocalPackage(source, module.DownloadUrl);
            bool validIcon = IsValidIcon(source, module.Icon);

            if (string.IsNullOrWhiteSpace(module.Id) || !ids.Add(module.Id) || !Version.TryParse(module.Version, out _) || module.ModuleApiVersion <= 0 || string.IsNullOrWhiteSpace(module.DisplayName) || string.IsNullOrWhiteSpace(module.Category) || !validPackageLocation || !validIcon || module.Size <= 0 || module.Sha256.Length != 64 || !module.Sha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("The module catalogue contains invalid metadata.");
            }
        }
    }

    private static bool IsValidOptionalResource(GlanceModuleFeedSource source, Uri? resourceUri) => resourceUri is null || resourceUri.Scheme == Uri.UriSchemeHttps || source.AllowLocalPackages && IsLocalPackage(source, resourceUri);

    private static bool IsValidIcon(GlanceModuleFeedSource source, GlanceModuleFeedIcon? icon)
    {
        if (icon is null || string.IsNullOrWhiteSpace(icon.Source) || !IsValidOptionalColor(icon.AccentColor) || !IsValidOptionalColor(icon.LightAccentColor))
        {
            return false;
        }

        return icon.Type switch
        {
            GlanceModuleIconType.Glyph => !string.IsNullOrWhiteSpace(icon.FontFamily),
            GlanceModuleIconType.Path => IsValidOptionalPathData(icon.Source) && IsValidOptionalPathData(icon.LightSource),
            GlanceModuleIconType.Bitmap => Uri.TryCreate(icon.Source, UriKind.Absolute, out Uri? iconUri) && IsValidOptionalResource(source, iconUri) && (string.IsNullOrWhiteSpace(icon.LightSource) || Uri.TryCreate(icon.LightSource, UriKind.Absolute, out Uri? lightIconUri) && IsValidOptionalResource(source, lightIconUri)),
            _ => false
        };
    }

    private static bool IsValidOptionalPathData(string value) => string.IsNullOrWhiteSpace(value) || value.Length <= 32768 && !value.Any(char.IsControl);

    private static bool IsValidOptionalColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string color = value.TrimStart('#');
        return color.Length is 6 or 8 && color.All(Uri.IsHexDigit);
    }

    private static bool IsLocalPackage(GlanceModuleFeedSource source, Uri packageUri)
    {
        if (!source.Uri.IsFile || !packageUri.IsFile)
        {
            return false;
        }

        string sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(source.Uri.LocalPath)!);
        string packagePath = Path.GetFullPath(packageUri.LocalPath);
        return packagePath.StartsWith(sourceDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
