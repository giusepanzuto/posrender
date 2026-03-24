using Posrender.Parsing;
using Posrender.Rendering;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using System;
using System.IO;

namespace Posrender;

/// <summary>
/// Entry point for rendering ESC/POS byte streams to PNG images.
/// </summary>
public static class PosRenderer
{
    /// <summary>
    /// Parses <paramref name="data"/> as an ESC/POS command stream and returns
    /// a <see cref="MemoryStream"/> containing the rendered PNG image.
    /// </summary>
    /// <param name="data">Raw ESC/POS byte array.</param>
    /// <param name="options">Render options (paper width, DPI). Uses defaults when null.</param>
    /// <returns>A <see cref="MemoryStream"/> positioned at offset 0 containing the PNG.</returns>
    public static Stream Render(byte[] data, PosRenderOptions? options = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        options ??= new PosRenderOptions();

        var commands = EscPosParser.Parse(data);
        var renderer = new EscPosImageRenderer(options.PaperWidthDots);

        using var image = renderer.Render(commands);
        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }
}
