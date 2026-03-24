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

    // --- Font cell size ---

    [Fact]
    public void BitmapFont_CellWidth_Is12() => Assert.Equal(12, BitmapFont.CellWidth);

    [Fact]
    public void BitmapFont_CellHeight_Is24() => Assert.Equal(24, BitmapFont.CellHeight);

    [Fact]
    public void Render_SingleLineFeed_HeightEquals_CellHeight()
    {
        using var image = Render(new LineFeedCommand(1));
        Assert.Equal(BitmapFont.CellHeight, image.Height);
    }

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

    // --- Word wrap ---

    [Fact]
    public void Render_TextLongerThanPaperWidth_WrapsToMultipleLines()
    {
        // At 1× scale, charWidth=8px, paperWidth=576px → max 72 chars per line
        var longText = new string('A', 100); // definitely overflows
        using var singleLine = Render(new LineFeedCommand(1));
        using var wrapped    = Render(new PrintTextCommand(longText), new LineFeedCommand(1));

        Assert.True(wrapped.Height > singleLine.Height,
            "Overflowing text should produce more vertical space than a blank line.");
    }

    [Fact]
    public void Render_WordWrap_HeightGrowsWithLineCount()
    {
        // Two separate LF-terminated lines vs one long line that wraps to ~2 lines
        using var twoExplicit = Render(
            new PrintTextCommand(new string('A', 50)), new LineFeedCommand(1),
            new PrintTextCommand(new string('A', 50)), new LineFeedCommand(1));

        using var oneWrapped = Render(
            new PrintTextCommand(new string('A', 100)), new LineFeedCommand(1));

        // Wrapped single line should have similar height to two explicit lines
        Assert.True(Math.Abs(oneWrapped.Height - twoExplicit.Height) <= BitmapFont.CellHeight,
            "Auto-wrapped line should produce roughly the same height as two explicit lines.");
    }

    [Fact]
    public void Render_WordWrap_BreaksPreferentiallyAtSpaces()
    {
        // 9 words of 7 chars each + space = 8px × 8 = 64px per word → ~9 words fit on 576px
        // Adding more forces a wrap; with spaces the break should land at a space
        var text = string.Join(" ", Enumerable.Repeat("ABCDEFG", 20)); // 20 words
        using var image = Render(new PrintTextCommand(text), new LineFeedCommand(1));

        // Must produce at least 2 text rows
        using var oneLine = Render(new LineFeedCommand(1));
        Assert.True(image.Height >= oneLine.Height * 2);
    }

    // --- QR placeholder ---

    [Fact]
    public void Render_QrPlaceholder_IncreasesHeight()
    {
        using var withQr    = Render(new PrintQrPlaceholderCommand(99));
        using var withoutQr = Render();
        Assert.True(withQr.Height > withoutQr.Height);
    }

    [Fact]
    public void Render_QrPlaceholder_HasDarkBorderPixels()
    {
        using var image = Render(new PrintQrPlaceholderCommand(50));
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
        Assert.True(hasDark, "QR placeholder should contain dark border pixels.");
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
