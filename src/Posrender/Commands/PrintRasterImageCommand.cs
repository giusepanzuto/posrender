namespace Posrender.Commands;

/// <summary>GS v 0 — prints a raster bit image. Pixels is a 1-bit-per-pixel bitmap, row-major.</summary>
public sealed class PrintRasterImageCommand : IEscPosCommand
{
    /// <summary>Image width in dots.</summary>
    public int Width { get; }

    /// <summary>Image height in dots.</summary>
    public int Height { get; }

    /// <summary>
    /// Raw 1-bit pixel data, row-major. Each byte holds 8 horizontal pixels,
    /// MSB first. Length == ceil(Width / 8) * Height.
    /// </summary>
    public byte[] Pixels { get; }

    public PrintRasterImageCommand(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
