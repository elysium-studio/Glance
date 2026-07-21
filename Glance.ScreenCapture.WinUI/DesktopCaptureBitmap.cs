using System;

namespace Glance.ScreenCapture.WinUI;

internal sealed class DesktopCaptureBitmap
{
    public DesktopCaptureBitmap(int originX, int originY, int width, int height, byte[] pixels)
    {
        OriginX = originX;
        OriginY = originY;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int OriginX { get; }

    public int OriginY { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public NativeRectangle Bounds => new(OriginX, OriginY, Width, Height);

    public DesktopCaptureBitmap Crop(NativeRectangle rectangle)
    {
        NativeRectangle clipped = rectangle.Intersect(Bounds);

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

        return new DesktopCaptureBitmap(clipped.X, clipped.Y, clipped.Width, clipped.Height, croppedPixels);
    }
}

internal readonly record struct NativeRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public NativeRectangle Intersect(NativeRectangle other)
    {
        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return new NativeRectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
