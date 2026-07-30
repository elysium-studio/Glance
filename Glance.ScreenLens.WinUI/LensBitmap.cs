namespace Glance.ScreenLens.WinUI;

internal sealed class LensBitmap(int originX, int originY, int width, int height, byte[] pixels)
{
    public int OriginX { get; } = originX;

    public int OriginY { get; } = originY;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public byte[] Pixels { get; } = pixels;

    public LensRectangle Bounds => new(OriginX, OriginY, Width, Height);

    public LensBitmap Crop(LensRectangle rectangle)
    {
        LensRectangle clipped = rectangle.Intersect(Bounds);

        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        }

        byte[] croppedPixels = new byte[clipped.Width * clipped.Height * 4];
        int sourceX = clipped.X - OriginX;
        int sourceY = clipped.Y - OriginY;
        int sourceStride = Width * 4;
        int destinationStride = clipped.Width * 4;

        for (int row = 0; row < clipped.Height; row++)
        {
            Buffer.BlockCopy(Pixels, ((sourceY + row) * sourceStride) + (sourceX * 4), croppedPixels, row * destinationStride, destinationStride);
        }

        return new LensBitmap(clipped.X, clipped.Y, clipped.Width, clipped.Height, croppedPixels);
    }
}

internal readonly record struct LensRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public LensRectangle Intersect(LensRectangle other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return new LensRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
