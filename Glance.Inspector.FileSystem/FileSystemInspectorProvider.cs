using Glance.Application.Abstractions;
using System.Security.Cryptography;

namespace Glance.Inspector.FileSystem;

public sealed class FileSystemInspectorProvider(ModuleResourceTextLocalizer<FileSystemInspectorModule> localizer, IFolderSpaceAnalyzer folderSpaceAnalyzer) :
    IGlanceInspectorProvider
{
    private readonly ModuleResourceTextLocalizer<FileSystemInspectorModule> localizer = localizer;
    private readonly IFolderSpaceAnalyzer folderSpaceAnalyzer = folderSpaceAnalyzer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.FilesAndFolders", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

    public GlanceInspectorMatch Match(GlanceContentContext context) => context.Kind == GlanceContentKind.FilesAndFolders && context.StorageItems.Count > 0 ? GlanceInspectorMatch.Supported : GlanceInspectorMatch.None;

    public async Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default)
    {
        List<GlanceInspectionSection> sections = [];
        List<IGlanceInspectionAction> actions = [];

        foreach (GlanceStorageItem item in context.StorageItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sections.AddRange(await InspectItemAsync(item, cancellationToken));
        }

        if (context.StorageItems.Count == 1)
        {
            GlanceStorageItem item = context.StorageItems[0];
            actions.Add(new CopyPathInspectionAction(item.Path, localizer));
            actions.Add(new OpenLocationInspectionAction(item.Path, item.IsFolder, localizer));
        }

        return new GlanceInspectionResult(sections, actions);
    }

    private async Task<IReadOnlyList<GlanceInspectionSection>> InspectItemAsync(GlanceStorageItem item, CancellationToken cancellationToken)
    {
        List<GlanceInspectionProperty> properties =
        [
            new(localizer.GetText("Name"), item.Name),
            new(localizer.GetText("Type"), item.IsFolder ? localizer.GetText("Folder") : GetFileType(item.Path)),
            new(localizer.GetText("Location"), item.Path)
        ];

        if (item.IsFolder)
        {
            DirectoryInfo directory = new(item.Path);
            properties.Add(new GlanceInspectionProperty(localizer.GetText("Contains"), GetFolderSummary(directory)));
            properties.Add(new GlanceInspectionProperty(localizer.GetText("Created"), FormatDate(directory.CreationTime)));
            properties.Add(new GlanceInspectionProperty(localizer.GetText("Modified"), FormatDate(directory.LastWriteTime)));
            properties.Add(new GlanceInspectionProperty(localizer.GetText("Attributes"), directory.Attributes.ToString()));
            FolderSpaceAnalysis analysis = await folderSpaceAnalyzer.AnalyzeAsync(item.Path, cancellationToken);
            return [new GlanceInspectionSection(item.Name, properties), CreateSpaceUsageSection(item.Name, analysis)];
        }

        FileInfo file = new(item.Path);
        properties.Add(new GlanceInspectionProperty(localizer.GetText("Size"), FormatBytes(file.Length)));
        properties.Add(new GlanceInspectionProperty(localizer.GetText("Created"), FormatDate(file.CreationTime)));
        properties.Add(new GlanceInspectionProperty(localizer.GetText("Modified"), FormatDate(file.LastWriteTime)));
        properties.Add(new GlanceInspectionProperty(localizer.GetText("Attributes"), file.Attributes.ToString()));

        if (CanCalculateHash(file))
        {
            properties.Add(new GlanceInspectionProperty(localizer.GetText("Sha256"), await CalculateHashAsync(file.FullName, cancellationToken)));
        }

        return [new GlanceInspectionSection(item.Name, properties)];
    }

    private GlanceInspectionSection CreateSpaceUsageSection(string name, FolderSpaceAnalysis analysis)
    {
        List<GlanceInspectionDistributionItem> items = [.. analysis.Entries.Take(8).Select(entry => new GlanceInspectionDistributionItem(entry.Name, entry.Size, FormatBytes(entry.Size)))];

        if (analysis.Entries.Count > items.Count)
        {
            long otherSize = analysis.Entries.Skip(items.Count).Sum(entry => entry.Size);
            items.Add(new GlanceInspectionDistributionItem(localizer.GetText("Other"), otherSize, FormatBytes(otherSize)));
        }

        GlanceInspectionDistribution? distribution = analysis.TotalBytes > 0 ? new GlanceInspectionDistribution(items) : null;
        GlanceInspectionProperty[] properties =
        [
            new(localizer.GetText("TotalSize"), FormatBytes(analysis.TotalBytes)),
            new(localizer.GetText("FilesAnalysed"), analysis.FileCount.ToString("N0"))
        ];
        return new GlanceInspectionSection(string.Format(localizer.GetText("SpaceUsageTitle"), name), properties, distribution);
    }

    private static bool CanCalculateHash(FileInfo file) => file.Exists && file.Length <= 512L * 1024 * 1024;

    private static async Task<string> CalculateHashAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private string GetFolderSummary(DirectoryInfo directory)
    {
        try
        {
            int files = directory.EnumerateFiles().Count();
            int folders = directory.EnumerateDirectories().Count();
            return string.Format(localizer.GetText("FolderSummary"), files, folders);
        }
        catch
        {
            return localizer.GetText("Unavailable");
        }
    }

    private string GetFileType(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? localizer.GetText("File") : string.Format(localizer.GetText("FileType"), extension.TrimStart('.').ToUpperInvariant());
    }

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

    private static string FormatDate(DateTime date) => date.ToString("g");
}
