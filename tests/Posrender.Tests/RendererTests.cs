using Posrender.Commands;
using Posrender.Rendering;
using SixLabors.ImageSharp.PixelFormats;

namespace Posrender.Tests;

public class RendererTests
{
    private const int PaperWidth = 576;

    private static EscPosImageRenderer CreateRenderer() => new(PaperWidth);

    private static SixLabors.ImageSharp.Image<Rgb24> Render(params IEscPosCommand[] commands) =>
        CreateRenderer().Render(commands);

    // --- Canvas size ---

    [Fact]
    public void Render_NoCommands_ProducesImageWithCorrectWidth()
    {
        using var image = Render();
        Assert.Equal(PaperWidth, image.Width);
    }

    [Fact]
    public void Render_LineFeed_IncreasesImageHeight()
    {
        using var withLf   = Render(new LineFeedCommand(1));
        using var withoutLf = Render();
        Assert.True(withLf.Height >= withoutLf.Height);
    }

    [Fact]
    public void Render_MultipleLineFeed_HeightScalesWithLines()
    {
        using var one  = Render(new LineFeedCommand(1));
        using var two  = Render(new LineFeedCommand(2));
        Assert.True(two.Height > one.Height);
    }

    // --- Text produces dark pixels ---

    [Fact]
    public void Render_PrintText_ContainsDarkPixels()
    {
        using var image = Render(new PrintTextCommand("A"), new LineFeedCommand(1));
        bool hasDark = false;
        image.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height && !hasDark; y++)
            {
                var row = acc.GetRowSpan(y);
                foreach (var p in row)
                    if (p.R < 128) { hasDark = true; break; }
            }
        });
        Assert.True(hasDark, "Expected at least one dark pixel from text rendering.");
    }

    // --- Alignment ---

    [Fact]
    public void Render_LeftAlignment_FirstDarkPixelNearLeftEdge()
    {
        using var image = Render(
            new SetAlignmentCommand(TextAlignment.Left),
            new PrintTextCommand("X"),
            new LineFeedCommand(1));

        int firstDarkX = FindFirstDarkPixelX(image);
        Assert.True(firstDarkX < PaperWidth / 3, $"Left-aligned text should start near left; got x={firstDarkX}");
    }

    [Fact]
    public void Render_CenterAlignment_FirstDarkPixelNearCenter()
    {
        using var image = Render(
            new SetAlignmentCommand(TextAlignment.Center),
            new PrintTextCommand("X"),
            new LineFeedCommand(1));

        int firstDarkX = FindFirstDarkPixelX(image);
        Assert.True(firstDarkX > PaperWidth / 4, $"Center-aligned text should not start at far left; got x={firstDarkX}");
    }

    // --- Bold produces denser pixels ---

    [Fact]
    public void Render_Bold_HasMoreDarkPixelsThanNormal()
    {
        using var normal = Render(new PrintTextCommand("III"), new LineFeedCommand(1));
        using var bold   = Render(new SetBoldCommand(true), new PrintTextCommand("III"), new LineFeedCommand(1));

        Assert.True(CountDarkPixels(bold) >= CountDarkPixels(normal));
    }

    // --- Underline ---

    [Fact]
    public void Render_Underline_HasMoreDarkPixelsThanNormal()
    {
        using var normal    = Render(new PrintTextCommand("___"), new LineFeedCommand(1));
        using var underline = Render(new SetUnderlineCommand(UnderlineMode.Single), new PrintTextCommand("___"), new LineFeedCommand(1));

        Assert.True(CountDarkPixels(underline) >= CountDarkPixels(normal));
    }

    // --- Raster image ---

    [Fact]
    public void Render_RasterImage_IncreasesHeight()
    {
        var pixels = new byte[] { 0xFF }; // 8 dots wide, 1 row, all black
        using var withImg   = Render(new PrintRasterImageCommand(8, 1, pixels));
        using var withoutImg = Render();
        Assert.True(withImg.Height > withoutImg.Height);
    }

    [Fact]
    public void Render_RasterImage_BlackRow_ProducesDarkPixels()
    {
        var pixels = new byte[] { 0xFF }; // all 8 bits set
        using var image = Render(new PrintRasterImageCommand(8, 1, pixels));

        bool hasDark = false;
        image.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height && !hasDark; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < 8; x++)
                    if (row[x].R < 128) { hasDark = true; break; }
            }
        });
        Assert.True(hasDark);
    }

    // --- FontSize ---

    [Fact]
    public void Render_DoubleSizeText_TallerThanNormal()
    {
        using var normal = Render(new PrintTextCommand("A"), new LineFeedCommand(1));
        using var big    = Render(new SetFontSizeCommand(1, 2), new PrintTextCommand("A"), new LineFeedCommand(1));
        Assert.True(big.Height > normal.Height);
    }

    // --- Helpers ---

    private static int FindFirstDarkPixelX(SixLabors.ImageSharp.Image<Rgb24> image)
    {
        int result = image.Width;
        image.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    if (row[x].R < 128 && x < result) { result = x; break; }
            }
        });
        return result;
    }

    private static int CountDarkPixels(SixLabors.ImageSharp.Image<Rgb24> image)
    {
        int count = 0;
        image.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < acc.Height; y++)
            {
                var row = acc.GetRowSpan(y);
                foreach (var p in row)
                    if (p.R < 128) count++;
            }
        });
        return count;
    }
}
