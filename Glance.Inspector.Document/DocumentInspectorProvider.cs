using Glance.Application.Abstractions;
using System.IO.Compression;
using System.Xml.Linq;
using Windows.Data.Pdf;
using Windows.Storage;

namespace Glance.Inspector.Document;

public sealed class DocumentInspectorProvider(ModuleResourceTextLocalizer<DocumentInspectorModule> localizer) :
    IGlanceInspectorProvider
{
    private static readonly HashSet<string> officeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".pptx", ".xlsx" };
    private readonly ModuleResourceTextLocalizer<DocumentInspectorModule> localizer = localizer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.Documents", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

    public GlanceInspectorMatch Match(GlanceContentContext context) => GetMatch(context);

    public async Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default)
    {
        List<GlanceInspectionSection> sections = [];

        foreach (GlanceStorageItem item in context.StorageItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.IsFolder || !IsSupported(item.Path))
            {
                continue;
            }

            sections.Add(string.Equals(Path.GetExtension(item.Path), ".pdf", StringComparison.OrdinalIgnoreCase) ? await InspectPdfAsync(item) : InspectOfficeDocument(item));
        }

        return new GlanceInspectionResult(sections, []);
    }

    private async Task<GlanceInspectionSection> InspectPdfAsync(GlanceStorageItem item)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(item.Path);
        PdfDocument document = await PdfDocument.LoadFromFileAsync(file);
        List<GlanceInspectionProperty> properties =
        [
            new(localizer.GetText("Format"), "PDF"),
            new(localizer.GetText("Pages"), document.PageCount.ToString("N0")),
            new(localizer.GetText("PasswordProtected"), document.IsPasswordProtected ? localizer.GetText("Yes") : localizer.GetText("No"))
        ];
        return new GlanceInspectionSection(string.Format(localizer.GetText("SectionTitle"), item.Name), properties);
    }

    private GlanceInspectionSection InspectOfficeDocument(GlanceStorageItem item)
    {
        using ZipArchive archive = ZipFile.OpenRead(item.Path);
        Dictionary<string, string> core = ReadProperties(archive.GetEntry("docProps/core.xml"));
        Dictionary<string, string> extended = ReadProperties(archive.GetEntry("docProps/app.xml"));
        string extension = Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant();
        List<GlanceInspectionProperty> properties =
        [
            new(localizer.GetText("Format"), extension),
            new(localizer.GetText("Title"), GetValue(core, "title")),
            new(localizer.GetText("Author"), GetValue(core, "creator")),
            new(localizer.GetText("LastEditedBy"), GetValue(core, "lastModifiedBy")),
            new(localizer.GetText("Created"), GetValue(core, "created")),
            new(localizer.GetText("Modified"), GetValue(core, "modified"))
        ];

        AddExtendedProperty(properties, extended, "Pages", "Pages");
        AddExtendedProperty(properties, extended, "Slides", "Slides");
        AddExtendedProperty(properties, extended, "Words", "Words");
        AddExtendedProperty(properties, extended, "Application", "CreatedWith");
        return new GlanceInspectionSection(string.Format(localizer.GetText("SectionTitle"), item.Name), properties);
    }

    private void AddExtendedProperty(List<GlanceInspectionProperty> properties, IReadOnlyDictionary<string, string> values, string key, string resourceKey)
    {
        if (values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            properties.Add(new GlanceInspectionProperty(localizer.GetText(resourceKey), value));
        }
    }

    private string GetValue(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : localizer.GetText("Unknown");

    private static Dictionary<string, string> ReadProperties(ZipArchiveEntry? entry)
    {
        if (entry is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using Stream stream = entry.Open();
        XDocument document = XDocument.Load(stream);
        return document.Root?.Elements().Where(element => !string.IsNullOrWhiteSpace(element.Value)).GroupBy(element => element.Name.LocalName, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSupported(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) || officeExtensions.Contains(extension);
    }

    private static GlanceInspectorMatch GetMatch(GlanceContentContext context)
    {
        if (context.Kind != GlanceContentKind.FilesAndFolders || !context.StorageItems.Any(item => !item.IsFolder && IsSupported(item.Path)))
        {
            return GlanceInspectorMatch.None;
        }

        return context.StorageItems.All(item => !item.IsFolder && IsSupported(item.Path)) ? GlanceInspectorMatch.Exact : GlanceInspectorMatch.Supported;
    }
}
