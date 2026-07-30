using Microsoft.UI.Input;
using System;
using System.Runtime.InteropServices;
using WinRT;

namespace Glance.ColorPicker.WinUI;

internal static partial class EyedropperCursor
{
    private const int CursorSize = 32;
    private const int HotspotX = 4;
    private const int HotspotY = 27;
    private const string InputCursorRuntimeClass = "Microsoft.UI.Input.InputCursor";

    private static readonly Guid InputCursorStaticsInteropId = new("AC6F5065-90C4-46CE-BEB7-05E138E54117");

    public static InputCursor Create()
    {
        nint cursorHandle = CreateCursorHandle();

        try
        {
            using IObjectReference factory = ActivationFactory.Get(InputCursorRuntimeClass, InputCursorStaticsInteropId);
            return CreateFromHandle(factory, cursorHandle);
        }
        finally
        {
            _ = DestroyCursor(cursorHandle);
        }
    }

    private static unsafe InputCursor CreateFromHandle(IObjectReference factory, nint cursorHandle)
    {
        nint result = nint.Zero;
        nint* virtualTable = *(nint**)factory.ThisPtr;
        delegate* unmanaged[Stdcall]<nint, nint, nint*, int> createFromCursor =
            (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)virtualTable[6];
        int resultCode = createFromCursor(factory.ThisPtr, cursorHandle, &result);
        Marshal.ThrowExceptionForHR(resultCode);

        try
        {
            return MarshalInspectable<InputCursor>.FromAbi(result);
        }
        finally
        {
            MarshalInspectable<InputCursor>.DisposeAbi(result);
        }
    }

    private static unsafe nint CreateCursorHandle()
    {
        uint[] pixels = new uint[CursorSize * CursorSize];
        DrawLine(pixels, 4, 27, 19, 12, 6, 0xFFFFFFFF);
        DrawLine(pixels, 4, 27, 19, 12, 3, 0xFF151515);
        DrawLine(pixels, 17, 7, 24, 14, 8, 0xFFFFFFFF);
        DrawLine(pixels, 17, 7, 24, 14, 5, 0xFF151515);
        DrawLine(pixels, 21, 3, 28, 10, 5, 0xFFFFFFFF);
        DrawLine(pixels, 21, 3, 28, 10, 3, 0xFF151515);
        SetPixel(pixels, 4, 27, 0xFFFFFFFF);
        SetPixel(pixels, 5, 26, 0xFF151515);

        nint colorBitmap;

        fixed (uint* pixelData = pixels)
        {
            colorBitmap = CreateBitmap(CursorSize, CursorSize, 1, 32, pixelData);
        }

        nint maskBitmap = CreateBitmap(CursorSize, CursorSize, 1, 1, null);

        if (colorBitmap == nint.Zero || maskBitmap == nint.Zero)
        {
            DeleteBitmap(colorBitmap);
            DeleteBitmap(maskBitmap);
            throw new InvalidOperationException("The eyedropper cursor bitmap could not be created.");
        }

        IconInfo information = new()
        {
            IsIcon = 0,
            HotspotX = HotspotX,
            HotspotY = HotspotY,
            MaskBitmap = maskBitmap,
            ColorBitmap = colorBitmap
        };

        nint cursorHandle = CreateIconIndirect(in information);
        DeleteBitmap(colorBitmap);
        DeleteBitmap(maskBitmap);

        if (cursorHandle == nint.Zero)
        {
            throw new InvalidOperationException("The eyedropper cursor could not be created.");
        }

        return cursorHandle;
    }

    private static void DrawLine(uint[] pixels, int startX, int startY, int endX, int endY, int thickness, uint color)
    {
        double radius = thickness / 2d;
        double lengthSquared = Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2);

        for (int y = 0; y < CursorSize; y++)
        {
            for (int x = 0; x < CursorSize; x++)
            {
                double position = lengthSquared == 0
                    ? 0
                    : (((x - startX) * (endX - startX)) + ((y - startY) * (endY - startY))) / lengthSquared;
                position = Math.Clamp(position, 0, 1);
                double nearestX = startX + (position * (endX - startX));
                double nearestY = startY + (position * (endY - startY));
                double distance = Math.Sqrt(Math.Pow(x - nearestX, 2) + Math.Pow(y - nearestY, 2));

                if (distance <= radius)
                {
                    pixels[(y * CursorSize) + x] = color;
                }
            }
        }
    }

    private static void SetPixel(uint[] pixels, int x, int y, uint color) =>
        pixels[(y * CursorSize) + x] = color;

    private static void DeleteBitmap(nint bitmap)
    {
        if (bitmap != nint.Zero)
        {
            _ = DeleteObject(bitmap);
        }
    }

    [LibraryImport("gdi32.dll")]
    private static unsafe partial nint CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, void* bits);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint value);

    [LibraryImport("user32.dll")]
    private static partial nint CreateIconIndirect(in IconInfo information);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyCursor(nint cursor);

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public int IsIcon;

        public int HotspotX;

        public int HotspotY;

        public nint MaskBitmap;

        public nint ColorBitmap;
    }
}
