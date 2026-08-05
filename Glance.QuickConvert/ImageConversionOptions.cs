namespace Glance.QuickConvert;

public sealed record ImageConversionOptions(string Format,
    ImageScaleMode ScaleMode,
    double Percentage,
    uint MaximumWidth,
    uint MaximumHeight,
    double Quality);
