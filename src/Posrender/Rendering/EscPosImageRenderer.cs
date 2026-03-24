using Posrender.Commands;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;

namespace Posrender.Rendering;

/// <summary>
/// Renders a sequence of parsed ESC/POS commands into an <see cref="Image{Rgb24}"/>.
/// </summary>
internal sealed class EscPosImageRenderer
{
    private readonly int _paperWidthDots;

    // Rendering state
    private bool _bold;
    private UnderlineMode _underline;
    private TextAlignment _alignment;
    private int _widthMultiplier = 1;
    private int _heightMultiplier = 1;

    // Lines already fully committed (after each LF)
    private readonly List<byte[]> _rows = new();

    // Text pending on the current line (not yet LF'd)
    private readonly List<(string text, bool bold, UnderlineMode underline, TextAlignment alignment, int wMul, int hMul)> _pendingSegments = new();

    public EscPosImageRenderer(int paperWidthDots)
    {
        _paperWidthDots = paperWidthDots;
    }

    public Image<Rgb24> Render(IReadOnlyList<IEscPosCommand> commands)
    {
        foreach (var cmd in commands)
        {
            switch (cmd)
            {
                case InitializeCommand:
                    Reset();
                    break;

                case SetAlignmentCommand a:
                    _alignment = a.Alignment;
                    break;

                case SetBoldCommand b:
                    _bold = b.On;
                    break;

                case SetUnderlineCommand u:
                    _underline = u.Mode;
                    break;

                case SetFontSizeCommand s:
                    _widthMultiplier = s.WidthMultiplier;
                    _heightMultiplier = s.HeightMultiplier;
                    break;

                case SetFontCommand:
                    // Font A/B affects char pitch; simplified — ignored in bitmap rendering
                    break;

                case PrintTextCommand t:
                    _pendingSegments.Add((t.Text, _bold, _underline, _alignment, _widthMultiplier, _heightMultiplier));
                    break;

                case LineFeedCommand lf:
                    for (int n = 0; n < lf.Lines; n++)
                        CommitLine();
                    break;

                case PrintRasterImageCommand img:
                    CommitLine(); // flush any pending text first
                    BlitRasterImage(img);
                    break;

                case PrintQrPlaceholderCommand qr:
                    CommitLine();
                    BlitQrPlaceholder(qr);
                    break;
            }
        }

        // Flush any remaining text
        if (_pendingSegments.Count > 0)
            CommitLine();

        return BuildImage();
    }

    private void Reset()
    {
        _bold = false;
        _underline = UnderlineMode.None;
        _alignment = TextAlignment.Left;
        _widthMultiplier = 1;
        _heightMultiplier = 1;
        _pendingSegments.Clear();
    }

    // --- Line commitment ---

    private void CommitLine()
    {
        foreach (var line in WrapSegments())
        {
            int cellH = BitmapFont.CellHeight * MaxHeightMul(line);
            var linePixels = RenderTextLine(cellH, line);
            for (int row = 0; row < cellH; row++)
                _rows.Add(linePixels[row]);
        }
        _pendingSegments.Clear();
    }

    private static int MaxHeightMul(List<(string text, bool bold, UnderlineMode underline, TextAlignment alignment, int wMul, int hMul)> segments)
    {
        int max = 1;
        foreach (var seg in segments)
            if (seg.hMul > max) max = seg.hMul;
        return max;
    }

    /// <summary>
    /// Splits <see cref="_pendingSegments"/> into wrapped lines that each fit within
    /// <see cref="_paperWidthDots"/>. Breaks preferentially at space characters.
    /// </summary>
    private IEnumerable<List<(string text, bool bold, UnderlineMode underline, TextAlignment alignment, int wMul, int hMul)>> WrapSegments()
    {
        var currentLine = new List<(string text, bool bold, UnderlineMode underline, TextAlignment alignment, int wMul, int hMul)>();
        int currentX = 0;

        foreach (var seg in _pendingSegments)
        {
            int charW = BitmapFont.CellWidth * seg.wMul;
            string remaining = seg.text;

            while (remaining.Length > 0)
            {
                int spaceLeft = _paperWidthDots - currentX;
                int maxChars  = charW > 0 ? spaceLeft / charW : remaining.Length;

                if (maxChars <= 0 && currentX > 0)
                {
                    // Current line full — emit and start a new one
                    yield return currentLine;
                    currentLine = new();
                    currentX    = 0;
                    maxChars    = _paperWidthDots / charW;
                }

                if (maxChars >= remaining.Length)
                {
                    // Entire remaining text fits on this line
                    currentLine.Add((remaining, seg.bold, seg.underline, seg.alignment, seg.wMul, seg.hMul));
                    currentX += remaining.Length * charW;
                    remaining = "";
                }
                else
                {
                    // Need to break — prefer a space boundary
                    int breakAt = remaining.LastIndexOf(' ', maxChars - 1, maxChars);
                    if (breakAt <= 0) breakAt = maxChars; // hard break if no space

                    if (breakAt > 0)
                        currentLine.Add((remaining[..breakAt].TrimEnd(), seg.bold, seg.underline, seg.alignment, seg.wMul, seg.hMul));

                    yield return currentLine;
                    currentLine = new();
                    currentX    = 0;
                    remaining   = remaining[breakAt..].TrimStart();
                }
            }
        }

        // Emit the last line (may be empty for a bare LF with no text)
        yield return currentLine;
    }

    /// <summary>Returns one row of pixels per scan line of the text line.</summary>
    private byte[][] RenderTextLine(int lineHeight, List<(string text, bool bold, UnderlineMode underline, TextAlignment alignment, int wMul, int hMul)> segments)
    {
        // Build the full pixel row buffer (width × lineHeight), all white
        var pixels = new byte[lineHeight][];
        for (int r = 0; r < lineHeight; r++)
        {
            pixels[r] = new byte[_paperWidthDots * 3]; // RGB
            for (int i = 0; i < pixels[r].Length; i++) pixels[r][i] = 0xFF; // white
        }

        if (segments.Count == 0)
            return pixels;

        // Measure total text width to compute alignment offset
        int totalTextWidth = 0;
        foreach (var seg in segments)
            totalTextWidth += seg.text.Length * BitmapFont.CellWidth * seg.wMul;

        int xOffset = segments[0].alignment switch
        {
            TextAlignment.Center => (_paperWidthDots - totalTextWidth) / 2,
            TextAlignment.Right  => _paperWidthDots - totalTextWidth,
            _                    => 0,
        };
        if (xOffset < 0) xOffset = 0;

        int x = xOffset;
        foreach (var (text, bold, underline, _, wMul, hMul) in segments)
        {
            int cellW = BitmapFont.CellWidth * wMul;
            int cellH = BitmapFont.CellHeight * hMul;
            int yOffset = lineHeight - cellH; // bottom-align within mixed-size line

            foreach (char c in text)
            {
                var glyph = BitmapFont.GetGlyph(c);

                for (int gy = 0; gy < BitmapFont.CharHeight; gy++)
                {
                    // Scale glyph rows: CharHeight(8) rows → CellHeight(24) rows → × hMul
                    int yBase = gy * BitmapFont.CellHeight / BitmapFont.CharHeight;
                    int yNext = (gy + 1) * BitmapFont.CellHeight / BitmapFont.CharHeight;

                    byte rowByte = glyph[gy];
                    for (int gx = 0; gx < BitmapFont.CharWidth; gx++)
                    {
                        // Scale glyph columns: CharWidth(8) cols → CellWidth(12) cols → × wMul
                        int xBase = gx * BitmapFont.CellWidth / BitmapFont.CharWidth;
                        int xNext = (gx + 1) * BitmapFont.CellWidth / BitmapFont.CharWidth;

                        // Font data uses LSB-left convention (bit 0 = leftmost pixel)
                        bool ink = (rowByte & (0x01 << gx)) != 0;
                        if (bold && !ink && gx > 0)
                            ink = (rowByte & (0x01 << (gx - 1))) != 0; // simple bold: repeat 1px right

                        if (!ink) continue;

                        for (int dy = 0; dy < (yNext - yBase) * hMul; dy++)
                        {
                            int py = yOffset + yBase * hMul + dy;
                            if (py >= lineHeight) continue;
                            byte[] rowPixels = pixels[py];

                            for (int dx = 0; dx < (xNext - xBase) * wMul; dx++)
                            {
                                int px = x + xBase * wMul + dx;
                                if (px >= _paperWidthDots) continue;
                                int idx = px * 3;
                                rowPixels[idx] = 0x00;     // R
                                rowPixels[idx + 1] = 0x00; // G
                                rowPixels[idx + 2] = 0x00; // B
                            }
                        }
                    }
                }

                // Underline
                if (underline != UnderlineMode.None)
                {
                    int thickness = underline == UnderlineMode.Double ? 2 : 1;
                    int ulY = yOffset + cellH - 1;
                    for (int t = 0; t < thickness; t++)
                    {
                        int py = ulY - t;
                        if (py < 0 || py >= lineHeight) continue;
                        for (int px = x; px < x + cellW && px < _paperWidthDots; px++)
                        {
                            int idx = px * 3;
                            pixels[py][idx] = 0x00;
                            pixels[py][idx + 1] = 0x00;
                            pixels[py][idx + 2] = 0x00;
                        }
                    }
                }

                x += cellW;
                if (x >= _paperWidthDots) break;
            }
        }

        return pixels;
    }

    // --- Raster image blitting ---

    private void BlitRasterImage(PrintRasterImageCommand img)
    {
        int bytesPerRow = (img.Width + 7) / 8;

        for (int gy = 0; gy < img.Height; gy++)
        {
            var rowPixels = new byte[_paperWidthDots * 3];
            for (int i = 0; i < rowPixels.Length; i++) rowPixels[i] = 0xFF; // white

            for (int bx = 0; bx < bytesPerRow; bx++)
            {
                int dataIdx = gy * bytesPerRow + bx;
                if (dataIdx >= img.Pixels.Length) break;
                byte b = img.Pixels[dataIdx];

                for (int bit = 0; bit < 8; bit++)
                {
                    int px = bx * 8 + bit;
                    if (px >= _paperWidthDots) break;
                    bool ink = (b & (0x80 >> bit)) != 0;
                    if (!ink) continue;
                    int idx = px * 3;
                    rowPixels[idx] = 0x00;
                    rowPixels[idx + 1] = 0x00;
                    rowPixels[idx + 2] = 0x00;
                }
            }

            _rows.Add(rowPixels);
        }
    }

    // --- QR code placeholder ---

    private void BlitQrPlaceholder(PrintQrPlaceholderCommand qr)
    {
        int size = Math.Min(qr.SizeDots, _paperWidthDots);
        int xOffset = _alignment switch
        {
            TextAlignment.Center => (_paperWidthDots - size) / 2,
            TextAlignment.Right  => _paperWidthDots - size,
            _                    => 0,
        };
        if (xOffset < 0) xOffset = 0;

        for (int gy = 0; gy < size; gy++)
        {
            var rowPixels = new byte[_paperWidthDots * 3];
            for (int k = 0; k < rowPixels.Length; k++) rowPixels[k] = 0xFF;

            bool isHorizontalEdge = gy == 0 || gy == size - 1;
            for (int gx = 0; gx < size; gx++)
            {
                int px = xOffset + gx;
                if (px >= _paperWidthDots) break;
                if (!isHorizontalEdge && gx != 0 && gx != size - 1) continue;
                int idx = px * 3;
                rowPixels[idx] = 0x00;
                rowPixels[idx + 1] = 0x00;
                rowPixels[idx + 2] = 0x00;
            }
            _rows.Add(rowPixels);
        }
    }

    // --- Final image assembly ---

    private Image<Rgb24> BuildImage()
    {
        int height = _rows.Count > 0 ? _rows.Count : 1;
        var image = new Image<Rgb24>(_paperWidthDots, height);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < _rows.Count; y++)
            {
                var rowSpan = accessor.GetRowSpan(y);
                byte[] src = _rows[y];
                for (int x = 0; x < _paperWidthDots; x++)
                {
                    int idx = x * 3;
                    rowSpan[x] = new Rgb24(src[idx], src[idx + 1], src[idx + 2]);
                }
            }
            // If no rows: leave the single white row as default
        });

        return image;
    }
}
