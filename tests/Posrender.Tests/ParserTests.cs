using Posrender.Commands;
using Posrender.Parsing;

namespace Posrender.Tests;

public class ParserTests
{
    // --- Helper ---
    private static IReadOnlyList<IEscPosCommand> Parse(params byte[] data) =>
        EscPosParser.Parse(data);

    // --- Empty / plain text ---

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptyList()
    {
        var result = Parse();
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PlainAsciiText_ReturnsPrintTextCommand()
    {
        var result = Parse(0x48, 0x69); // "Hi"
        var cmd = Assert.Single(result);
        var text = Assert.IsType<PrintTextCommand>(cmd);
        Assert.Equal("Hi", text.Text);
    }

    // --- ESC @ ---

    [Fact]
    public void Parse_EscAt_ReturnsInitializeCommand()
    {
        var result = Parse(0x1B, 0x40);
        var cmd = Assert.Single(result);
        Assert.IsType<InitializeCommand>(cmd);
    }

    // --- LF ---

    [Fact]
    public void Parse_LineFeed_ReturnsLineFeedCommand()
    {
        var result = Parse(0x0A);
        var cmd = Assert.Single(result);
        var lf = Assert.IsType<LineFeedCommand>(cmd);
        Assert.Equal(1, lf.Lines);
    }

    // --- ESC a ---

    [Theory]
    [InlineData(0, TextAlignment.Left)]
    [InlineData(1, TextAlignment.Center)]
    [InlineData(2, TextAlignment.Right)]
    public void Parse_EscA_ReturnsCorrectAlignment(byte n, TextAlignment expected)
    {
        var result = Parse(0x1B, 0x61, n);
        var cmd = Assert.Single(result);
        var align = Assert.IsType<SetAlignmentCommand>(cmd);
        Assert.Equal(expected, align.Alignment);
    }

    // --- ESC E ---

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Parse_EscE_ReturnsBoldCommand(byte n, bool expectedOn)
    {
        var result = Parse(0x1B, 0x45, n);
        var cmd = Assert.Single(result);
        var bold = Assert.IsType<SetBoldCommand>(cmd);
        Assert.Equal(expectedOn, bold.On);
    }

    // --- ESC - ---

    [Theory]
    [InlineData(0, UnderlineMode.None)]
    [InlineData(1, UnderlineMode.Single)]
    [InlineData(2, UnderlineMode.Double)]
    public void Parse_EscMinus_ReturnsUnderlineCommand(byte n, UnderlineMode expected)
    {
        var result = Parse(0x1B, 0x2D, n);
        var cmd = Assert.Single(result);
        var ul = Assert.IsType<SetUnderlineCommand>(cmd);
        Assert.Equal(expected, ul.Mode);
    }

    // --- GS ! ---

    [Fact]
    public void Parse_GsExclamation_ReturnsFontSizeCommand()
    {
        // n = 0x11 → high nibble 1 → wMul=2, low nibble 1 → hMul=2
        var result = Parse(0x1D, 0x21, 0x11);
        var cmd = Assert.Single(result);
        var size = Assert.IsType<SetFontSizeCommand>(cmd);
        Assert.Equal(2, size.WidthMultiplier);
        Assert.Equal(2, size.HeightMultiplier);
    }

    [Fact]
    public void Parse_GsExclamation_ZeroByte_Returns1x1()
    {
        var result = Parse(0x1D, 0x21, 0x00);
        var size = Assert.IsType<SetFontSizeCommand>(Assert.Single(result));
        Assert.Equal(1, size.WidthMultiplier);
        Assert.Equal(1, size.HeightMultiplier);
    }

    // --- ESC M ---

    [Theory]
    [InlineData(0, PrinterFont.A)]
    [InlineData(1, PrinterFont.B)]
    public void Parse_EscM_ReturnsFontCommand(byte n, PrinterFont expected)
    {
        var result = Parse(0x1B, 0x4D, n);
        var cmd = Assert.Single(result);
        var font = Assert.IsType<SetFontCommand>(cmd);
        Assert.Equal(expected, font.Font);
    }

    // --- GS v 0 (raster image) ---

    [Fact]
    public void Parse_GsV0_ReturnsRasterImageCommand()
    {
        // 8 pixels wide (1 byte/row), 2 rows, pixels = 0xFF, 0x00
        var data = new byte[]
        {
            0x1D, 0x76, 0x30, 0x00,
            0x01, 0x00, // xL=1, xH=0  → bytesPerRow=1
            0x02, 0x00, // yL=2, yH=0  → rows=2
            0xFF, 0x00  // pixel data
        };
        var result = EscPosParser.Parse(data);
        var cmd = Assert.Single(result);
        var img = Assert.IsType<PrintRasterImageCommand>(cmd);
        Assert.Equal(8, img.Width);
        Assert.Equal(2, img.Height);
        Assert.Equal(new byte[] { 0xFF, 0x00 }, img.Pixels);
    }

    // --- GS ( k — QR code ---

    [Fact]
    public void Parse_QrPrintCommand_EmitsPrintQrPlaceholderCommand()
    {
        // GS ( k pL=3 pH=0 cn=0x31 fn=0x51 m=0x00 — print QR
        var data = new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x00 };
        var result = EscPosParser.Parse(data);
        var cmd = Assert.Single(result);
        Assert.IsType<PrintQrPlaceholderCommand>(cmd);
    }

    [Fact]
    public void Parse_QrModuleSizeThenPrint_PlaceholderSizeScalesWithModuleSize()
    {
        // GS ( k pL=3 pH=0 cn=0x31 fn=0x43 n=4 0x00 — set module size to 4
        // GS ( k pL=3 pH=0 cn=0x31 fn=0x51 m=0x00  — print
        var data = new byte[]
        {
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x04, 0x00, // module size = 4
            0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x00,        // print
        };
        var result = EscPosParser.Parse(data);
        var cmd = Assert.IsType<PrintQrPlaceholderCommand>(Assert.Single(result));
        // placeholder size must be larger than with default module size (3)
        var defaultData = new byte[] { 0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x00 };
        var defaultCmd = Assert.IsType<PrintQrPlaceholderCommand>(Assert.Single(EscPosParser.Parse(defaultData)));
        Assert.True(cmd.SizeDots > defaultCmd.SizeDots);
    }

    [Fact]
    public void Parse_QrStoreData_DoesNotLeakBytesAsText()
    {
        // GS ( k pL=5 pH=0 cn=0x31 fn=0x50 m=0x00 data="AB" — store QR data (5 payload bytes)
        // followed by a bare LF that must NOT be swallowed
        var data = new byte[] { 0x1D, 0x28, 0x6B, 0x05, 0x00, 0x31, 0x50, 0x00, 0x41, 0x42, 0x0A };
        var result = EscPosParser.Parse(data);
        // Everything up to and including the GS( payload is consumed; only the trailing LF remains
        Assert.Single(result);
        Assert.IsType<LineFeedCommand>(result[0]);
    }

    // --- Incomplete sequence robustness ---

    [Fact]
    public void Parse_IncompleteEscSequence_DoesNotThrow()
    {
        // Truncated ESC E (missing n)
        var ex = Record.Exception(() => Parse(0x1B, 0x45));
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_IncompleteRasterImage_DoesNotThrow()
    {
        // Header present but pixel data truncated
        var data = new byte[] { 0x1D, 0x76, 0x30, 0x00, 0x01, 0x00, 0x02, 0x00 }; // 2 bytes expected, 0 provided
        var ex = Record.Exception(() => EscPosParser.Parse(data));
        Assert.Null(ex);
    }

    // --- Text accumulated across multiple bytes ---

    [Fact]
    public void Parse_TextThenLF_ReturnsTwoCommands()
    {
        var result = Parse(0x41, 0x42, 0x0A); // "AB\n"
        Assert.Equal(2, result.Count);
        Assert.IsType<PrintTextCommand>(result[0]);
        Assert.IsType<LineFeedCommand>(result[1]);
    }

    [Fact]
    public void Parse_EscAtResetsAndFlushesPendingText()
    {
        // Text then ESC @: text should be emitted before initialize
        var result = Parse(0x48, 0x69, 0x1B, 0x40); // "Hi" + ESC @
        Assert.Equal(2, result.Count);
        Assert.IsType<PrintTextCommand>(result[0]);
        Assert.IsType<InitializeCommand>(result[1]);
    }
}
