using System.IO.Compression;
using System.Text.Json;

namespace Glance.Application.Abstractions;

public static class GlanceModulePackageReader
{
    public const string ManifestEntryName = "module.json";

    public static GlanceModulePackageManifest ReadManifest(string packagePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry entry = archive.GetEntry(ManifestEntryName) ?? throw new InvalidDataException("The module package does not contain module metadata.");
        using Stream stream = entry.Open();
        GlanceModulePackageManifest manifest = JsonSerializer.Deserialize<GlanceModulePackageManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("The module metadata is invalid.");
        Validate(manifest);
        return manifest;
    }

    public static bool TryReadManifest(string packagePath, out GlanceModulePackageManifest? manifest)
    {
        try
        {
            manifest = ReadManifest(packagePath);
            return true;
        }
        catch (InvalidDataException)
        {
            manifest = null;
            return false;
        }
    }

    public static void Validate(GlanceModulePackageManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.Id) || !Version.TryParse(manifest.Version, out _))
        {
            throw new InvalidDataException("The module metadata is invalid.");
        }

        if (manifest.ModuleApiVersion != GlanceModuleContract.CurrentVersion)
        {
            throw new InvalidDataException("The module requires a different version of Glance.");
        }
    }
}
