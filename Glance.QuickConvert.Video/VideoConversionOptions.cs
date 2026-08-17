namespace Glance.QuickConvert.Video;

public sealed record VideoConversionOptions(string Format,
    VideoScaleMode ScaleMode,
    double Percentage,
    uint Width,
    uint Height,
    VideoConversionQuality Quality);
