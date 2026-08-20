using Glance.Application.Abstractions;
using System.IO.Compression;

namespace Glance.Inspector.Archive;

public sealed class ArchiveInspectorProvider(ModuleResourceTextLocalizer<ArchiveInspectorModule> localizer) :
    IGlanceInspectorProvider
{
    private static readonly HashSet<string> supportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".glance", ".nupkg", ".zip" };
    private readonly ModuleResourceTextLocalizer<ArchiveInspectorModule> localizer = localizer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.Archives", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

    public GlanceInspectorMatch Match(GlanceContentContext context) => GetMatch(context);

    public Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default)
    {
        List<GlanceInspectionSection> sections = [];

        foreach (GlanceStorageItem item in context.StorageItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSupported(item))
            {
                continue;
            }

            using ZipArchive archive = ZipFile.OpenRead(item.Path);
            long expandedSize = archive.Entries.Sum(entry => entry.Length);
            long compressedSize = archive.Entries.Sum(entry => entry.CompressedLength);
            int folderCount = archive.Entries.Count(entry => string.IsNullOrEmpty(entry.Name));
            int fileCount = archive.Entries.Count - folderCount;
            List<GlanceInspectionProperty> properties =
            [
                new(localizer.GetText("Files"), fileCount.ToString("N0")),
                new(localizer.GetText("Folders"), folderCount.ToString("N0")),
                new(localizer.GetText("ExpandedSize"), FormatBytes(expandedSize)),
                new(localizer.GetText("CompressedSize"), FormatBytes(compressedSize)),
                new(localizer.GetText("SpaceSaved"), expandedSize == 0 ? "0%" : $"{Math.Max(0, 1 - compressedSize / (double)expandedSize):P0}")
            ];
            sections.Add(new GlanceInspectionSection(string.Format(localizer.GetText("SectionTitle"), item.Name), properties));
        }

        return Task.FromResult(new GlanceInspectionResult(sections, []));
    }

    private static GlanceInspectorMatch GetMatch(GlanceContentContext context)
    {
        if (context.Kind != GlanceContentKind.FilesAndFolders || !context.StorageItems.Any(IsSupported))
        {
            return GlanceInspectorMatch.None;
        }

        return context.StorageItems.All(IsSupported) ? GlanceInspectorMatch.Exact : GlanceInspectorMatch.Supported;
    }

    private static bool IsSupported(GlanceStorageItem item) => !item.IsFolder && supportedExtensions.Contains(Path.GetExtension(item.Path));

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int suffix = 0;

        while (size >= 1024 && suffix < suffixes.Length - 1)
        {
            size /= 1024;
            suffix++;
        }

        return suffix == 0 ? $"{bytes:N0} {suffixes[suffix]}" : $"{size:0.##} {suffixes[suffix]}";
    }
}
