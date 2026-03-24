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

    [Theory]
    [InlineData(58,  203, 464)]   // 58mm × 203 DPI ≈ 464 dot
    [InlineData(72,  203, 575)]   // 72mm × 203 DPI ≈ 575 dot
    [InlineData(80,  203, 639)]   // 80mm × 203 DPI ≈ 639 dot
    [InlineData(80,  300, 945)]   // 80mm × 300 DPI ≈ 945 dot
    [InlineData(112, 203, 895)]   // 112mm × 203 DPI ≈ 895 dot
    public void FromMillimeters_ComputesCorrectDotWidth(int mm, int dpi, int expectedDots)
    {
        var options = PosRenderOptions.FromMillimeters(mm, dpi);

        Assert.Equal(expectedDots, options.PaperWidthDots);
        Assert.Equal(dpi, options.Dpi);
    }

    [Fact]
    public void FromMillimeters_DefaultDpiIs203()
    {
        var options = PosRenderOptions.FromMillimeters(80);

        Assert.Equal(203, options.Dpi);
    }

    [Fact]
    public void Render_80mmAt203Dpi_ProducesCorrectWidth()
    {
        var options = PosRenderOptions.FromMillimeters(80);
        var stream = PosRenderer.Render(Array.Empty<byte>(), options);

        stream.Position = 0;
        using var image = Image.Load(stream);
        Assert.Equal(639, image.Width);
    }
}
