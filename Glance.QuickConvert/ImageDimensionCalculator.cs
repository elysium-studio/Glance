namespace Glance.QuickConvert;

public static class ImageDimensionCalculator
{
    public static (uint Width, uint Height) Calculate(uint width,
        uint height,
        ImageConversionOptions options)
    {
        if (width == 0 || height == 0)
        {
            return (width, height);
        }

        double scale = options.ScaleMode switch
        {
            ImageScaleMode.Percentage => Math.Clamp(options.Percentage, 1, 400) / 100,
            ImageScaleMode.FitWithin => Math.Min(1,
                Math.Min((double)Math.Max(1u, options.MaximumWidth) / width,
                    (double)Math.Max(1u, options.MaximumHeight) / height)),
            _ => 1
        };

        return (Math.Max(1u, (uint)Math.Round(width * scale)),
            Math.Max(1u, (uint)Math.Round(height * scale)));
    }
}
