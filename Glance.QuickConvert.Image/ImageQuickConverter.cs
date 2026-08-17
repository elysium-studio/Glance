using Glance.Application.Abstractions;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Glance.QuickConvert.Image;

public sealed class ImageQuickConverter(ModuleResourceTextLocalizer<ImageQuickConverterModule> localizer) :
    IGlanceQuickConverter
{
    private static readonly HashSet<string> supportedExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".bmp", ".gif", ".heic", ".heif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    ];

    private readonly ModuleResourceTextLocalizer<ImageQuickConverterModule> localizer = localizer;

    public GlanceQuickConverterDescriptor Descriptor => new("QuickConvert.Images",
        localizer.GetText("ImageConverterName"),
        localizer.GetText("ImageConverterDescription"));

    public GlanceQuickConverterMatch Match(GlanceContentContext context) =>
        context.Kind == GlanceContentKind.FilesAndFolders &&
        context.StorageItems.Count > 0 &&
        context.StorageItems.All(item => !item.IsFolder && supportedExtensions.Contains(Path.GetExtension(item.Path)))
            ? GlanceQuickConverterMatch.Exact
            : GlanceQuickConverterMatch.None;

    public IGlanceQuickConverterEditor CreateEditor(GlanceContentContext context) => new ImageQuickConverterEditor(localizer);

    public async Task<IReadOnlyList<GlanceQuickConversionResult>> ConvertAsync(GlanceQuickConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Options is not ImageConversionOptions options)
        {
            throw new ArgumentException("Image conversion options were not supplied.", nameof(request));
        }

        List<GlanceQuickConversionResult> results = [];

        foreach (GlanceStorageItem item in request.Content.StorageItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string outputPath = await ConvertAsync(item.Path, options, cancellationToken);
                results.Add(new GlanceQuickConversionResult(item.Path, outputPath, true));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new GlanceQuickConversionResult(item.Path, null, false, exception.Message));
            }
        }

        return results;
    }

    private static async Task<string> ConvertAsync(string sourcePath,
        ImageConversionOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StorageFile source = await StorageFile.GetFileFromPathAsync(sourcePath);
        string outputPath = QuickConvertFileName.Create(sourcePath, options.Format, File.Exists);
        StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outputPath)!);
        StorageFile output = await outputFolder.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.GenerateUniqueName);

        try
        {
            using IRandomAccessStream input = await source.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
            (uint width, uint height) = ImageDimensionCalculator.Calculate(decoder.PixelWidth, decoder.PixelHeight, options);
            BitmapTransform transform = new()
            {
                ScaledWidth = width,
                ScaledHeight = height,
                InterpolationMode = BitmapInterpolationMode.Fant
            };
            PixelDataProvider pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            using IRandomAccessStream destination = await output.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await CreateEncoderAsync(destination, options);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                width,
                height,
                decoder.DpiX,
                decoder.DpiY,
                pixels.DetachPixelData());
            await encoder.FlushAsync();
            return output.Path;
        }
        catch
        {
            await output.DeleteAsync(StorageDeleteOption.PermanentDelete);
            throw;
        }
    }

    private static Task<BitmapEncoder> CreateEncoderAsync(IRandomAccessStream stream,
        ImageConversionOptions options)
    {
        Guid encoderId = options.Format.ToLowerInvariant() switch
        {
            "bmp" => BitmapEncoder.BmpEncoderId,
            "gif" => BitmapEncoder.GifEncoderId,
            "jpg" or "jpeg" => BitmapEncoder.JpegEncoderId,
            "tif" or "tiff" => BitmapEncoder.TiffEncoderId,
            _ => BitmapEncoder.PngEncoderId
        };

        if (encoderId != BitmapEncoder.JpegEncoderId)
        {
            return BitmapEncoder.CreateAsync(encoderId, stream).AsTask();
        }

        BitmapPropertySet properties = new()
        {
            ["ImageQuality"] = new BitmapTypedValue((float)Math.Clamp(options.Quality, 0.1, 1), Windows.Foundation.PropertyType.Single)
        };
        return BitmapEncoder.CreateAsync(encoderId, stream, properties).AsTask();
    }
}
