using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Posrender.Tests;

public class PosRendererTests
{
    [Fact]
    public void Render_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PosRenderer.Render(null!));
    }

    [Fact]
    public void Render_EmptyData_ReturnsValidPngStream()
    {
        var stream = PosRenderer.Render(Array.Empty<byte>());

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.NotNull(image);
    }

    [Fact]
    public void Render_EmptyData_OutputWidthMatchesDefaultPaperWidth()
    {
        var stream = PosRenderer.Render(Array.Empty<byte>());

        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.Equal(new PosRenderOptions().PaperWidthDots, image.Width);
    }

    [Fact]
    public void Render_CustomPaperWidth_OutputWidthMatchesOption()
    {
        var options = new PosRenderOptions { PaperWidthDots = 384 };

        var stream = PosRenderer.Render(Array.Empty<byte>(), options);

        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.Equal(384, image.Width);
    }
}
