using Glance.Application.Abstractions;
using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace Glance.Inspector.Application;

public sealed class ApplicationInspectorProvider(ModuleResourceTextLocalizer<ApplicationInspectorModule> localizer) :
    IGlanceInspectorProvider
{
    private static readonly HashSet<string> supportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".dll", ".exe", ".ocx", ".scr", ".sys" };
    private readonly ModuleResourceTextLocalizer<ApplicationInspectorModule> localizer = localizer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.Applications", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

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

            FileVersionInfo version = FileVersionInfo.GetVersionInfo(item.Path);
            List<GlanceInspectionProperty> properties =
            [
                new(localizer.GetText("Description"), ValueOrUnknown(version.FileDescription)),
                new(localizer.GetText("Product"), ValueOrUnknown(version.ProductName)),
                new(localizer.GetText("Company"), ValueOrUnknown(version.CompanyName)),
                new(localizer.GetText("FileVersion"), ValueOrUnknown(version.FileVersion)),
                new(localizer.GetText("ProductVersion"), ValueOrUnknown(version.ProductVersion)),
                new(localizer.GetText("Architecture"), GetArchitecture(item.Path))
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

    private string ValueOrUnknown(string? value) => string.IsNullOrWhiteSpace(value) ? localizer.GetText("Unknown") : value;

    private string GetArchitecture(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader reader = new(stream);
            return reader.PEHeaders.CoffHeader.Machine.ToString();
        }
        catch
        {
            return localizer.GetText("Unknown");
        }
    }

}
