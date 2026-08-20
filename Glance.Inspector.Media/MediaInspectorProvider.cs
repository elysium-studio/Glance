using Glance.Application.Abstractions;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Glance.Inspector.Media;

public sealed class MediaInspectorProvider(ModuleResourceTextLocalizer<MediaInspectorModule> localizer) :
    IGlanceInspectorProvider
{
    private static readonly HashSet<string> audioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".aac", ".flac", ".m4a", ".mp3", ".ogg", ".wav", ".wma" };
    private static readonly HashSet<string> videoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".webm", ".wmv" };
    private readonly ModuleResourceTextLocalizer<MediaInspectorModule> localizer = localizer;

    public GlanceInspectorProviderDescriptor Descriptor => new("Inspector.Media", localizer.GetText("ProviderName"), localizer.GetText("ProviderDescription"));

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

            StorageFile file = await StorageFile.GetFileFromPathAsync(item.Path);
            sections.Add(audioExtensions.Contains(Path.GetExtension(item.Path)) ? await InspectAudioAsync(file) : await InspectVideoAsync(file));
        }

        return new GlanceInspectionResult(sections, []);
    }

    private async Task<GlanceInspectionSection> InspectAudioAsync(StorageFile file)
    {
        MusicProperties media = await file.Properties.GetMusicPropertiesAsync();
        List<GlanceInspectionProperty> properties =
        [
            new(localizer.GetText("Duration"), FormatDuration(media.Duration)),
            new(localizer.GetText("Bitrate"), FormatBitrate(media.Bitrate)),
            new(localizer.GetText("Title"), ValueOrUnknown(media.Title)),
            new(localizer.GetText("Artist"), ValueOrUnknown(media.Artist)),
            new(localizer.GetText("Album"), ValueOrUnknown(media.Album)),
            new(localizer.GetText("Track"), media.TrackNumber == 0 ? localizer.GetText("Unknown") : media.TrackNumber.ToString())
        ];
        return new GlanceInspectionSection(string.Format(localizer.GetText("AudioSectionTitle"), file.Name), properties);
    }

    private async Task<GlanceInspectionSection> InspectVideoAsync(StorageFile file)
    {
        VideoProperties media = await file.Properties.GetVideoPropertiesAsync();
        List<GlanceInspectionProperty> properties =
        [
            new(localizer.GetText("Duration"), FormatDuration(media.Duration)),
            new(localizer.GetText("Dimensions"), string.Format(localizer.GetText("DimensionsValue"), media.Width, media.Height)),
            new(localizer.GetText("Bitrate"), FormatBitrate(media.Bitrate)),
            new(localizer.GetText("Title"), ValueOrUnknown(media.Title))
        ];
        return new GlanceInspectionSection(string.Format(localizer.GetText("VideoSectionTitle"), file.Name), properties);
    }

    private string ValueOrUnknown(string? value) => string.IsNullOrWhiteSpace(value) ? localizer.GetText("Unknown") : value;

    private string FormatBitrate(uint bitrate) => bitrate == 0 ? localizer.GetText("Unknown") : string.Format(localizer.GetText("BitrateValue"), bitrate / 1000d);

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");

    private static bool IsSupported(string path)
    {
        string extension = Path.GetExtension(path);
        return audioExtensions.Contains(extension) || videoExtensions.Contains(extension);
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
