using SixLabors.ImageSharp;

namespace Posrender.Tests;

public class IntegrationTests
{
    // --- Basic end-to-end ---

    [Fact]
    public void Render_RealisticSequence_ReturnsValidPng()
    {
        // ESC @ + alignment center + "HELLO" + LF + alignment left + "World" + LF
        byte[] data =
        [
            0x1B, 0x40,              // ESC @ — init
            0x1B, 0x61, 0x01,        // ESC a 1 — center
            0x48, 0x45, 0x4C, 0x4C, 0x4F, // "HELLO"
            0x0A,                    // LF
            0x1B, 0x61, 0x00,        // ESC a 0 — left
            0x57, 0x6F, 0x72, 0x6C, 0x64, // "World"
            0x0A,                    // LF
        ];

        var stream = PosRenderer.Render(data);

        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.Equal(new PosRenderOptions().PaperWidthDots, image.Width);
        Assert.True(image.Height > 0);
    }

    [Fact]
    public void Render_BoldTextSequence_ProducesLargerPng()
    {
        byte[] normal = [0x41, 0x41, 0x41, 0x0A]; // "AAA\n"
        byte[] bold   = [0x1B, 0x45, 0x01, 0x41, 0x41, 0x41, 0x0A]; // ESC E 1 + "AAA\n"

        var s1 = PosRenderer.Render(normal);
        var s2 = PosRenderer.Render(bold);

        // Both should be valid PNGs
        s1.Position = 0; s2.Position = 0;
        Image.Load(s1).Dispose();
        Image.Load(s2).Dispose();
    }

    [Fact]
    public void Render_RasterImageSequence_ProducesCorrectWidth()
    {
        // GS v 0 — 8 dots wide (1 byte/row), 4 rows, all black
        byte[] data =
        [
            0x1D, 0x76, 0x30, 0x00,
            0x01, 0x00, // xL=1 xH=0
            0x04, 0x00, // yL=4 yH=0
            0xFF, 0xFF, 0xFF, 0xFF  // 4 rows × 8 black dots
        ];

        var options = new PosRenderOptions { PaperWidthDots = 384 };
        var stream = PosRenderer.Render(data, options);

        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.Equal(384, image.Width);
        Assert.True(image.Height >= 4);
    }

    [Fact]
    public void Render_DoubleSizeText_ProducesTallerImage()
    {
        byte[] normal = [0x41, 0x0A];
        byte[] dbl    = [0x1D, 0x21, 0x11, 0x41, 0x0A]; // GS ! 0x11 → 2×2

        var s1 = PosRenderer.Render(normal);
        var s2 = PosRenderer.Render(dbl);

        s1.Position = 0; s2.Position = 0;
        using var img1 = Image.Load(s1);
        using var img2 = Image.Load(s2);
        Assert.True(img2.Height > img1.Height);
    }

    [Fact]
    public void Render_OutputStreamIsRewindable()
    {
        var stream = PosRenderer.Render([0x41, 0x0A]);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanSeek);
        Assert.True(stream.Length > 0);
    }
}
