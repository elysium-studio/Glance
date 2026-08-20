using Glance.Application.Abstractions;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Glance.Inspector.Image;

public sealed class ImageInspectorProvider(ModuleResourceTextLocalizer<ImageInspectorModule> localizer) :
    IGlanceInspectorProvider
{
    private static readonly HashSet<string> supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };

    private readonly ModuleResourceTextLocalizer<ImageInspectorModule> localizer = localizer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.Images", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

    public GlanceInspectorMatch Match(GlanceContentContext context) => GetMatch(context);

    public async Task<GlanceInspectionResult> InspectAsync(GlanceContentContext context, CancellationToken cancellationToken = default)
    {
        List<GlanceInspectionSection> sections = [];

        foreach (GlanceStorageItem item in context.StorageItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSupported(item))
            {
                continue;
            }

            StorageFile file = await StorageFile.GetFileFromPathAsync(item.Path);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            BitmapPropertySet metadata = await decoder.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation", "System.Image.HorizontalResolution", "System.Image.VerticalResolution"]);
            List<GlanceInspectionProperty> properties =
            [
                new(localizer.GetText("Dimensions"), string.Format(localizer.GetText("DimensionsValue"), decoder.PixelWidth, decoder.PixelHeight)),
                new(localizer.GetText("Format"), Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant()),
                new(localizer.GetText("ColourSpace"), decoder.BitmapPixelFormat.ToString()),
                new(localizer.GetText("Alpha"), decoder.BitmapAlphaMode == BitmapAlphaMode.Ignore ? localizer.GetText("No") : localizer.GetText("Yes")),
                new(localizer.GetText("Dpi"), string.Format(localizer.GetText("DpiValue"), decoder.DpiX, decoder.DpiY))
            ];

            if (metadata.TryGetValue("System.Photo.Orientation", out BitmapTypedValue? orientation) && orientation.Value is not null)
            {
                properties.Add(new GlanceInspectionProperty(localizer.GetText("Orientation"), orientation.Value.ToString()!));
            }

            sections.Add(new GlanceInspectionSection(string.Format(localizer.GetText("SectionTitle"), item.Name), properties));
        }

        return new GlanceInspectionResult(sections, []);
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
}
