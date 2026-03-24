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
        int cellH = BitmapFont.CharHeight * (_pendingSegments.Count > 0 ? MaxHeightMul() : 1);
        var linePixels = RenderTextLine(cellH);
        for (int row = 0; row < cellH; row++)
            _rows.Add(linePixels[row]);
        _pendingSegments.Clear();
    }

    private int MaxHeightMul()
    {
        int max = 1;
        foreach (var seg in _pendingSegments)
            if (seg.hMul > max) max = seg.hMul;
        return max;
    }

    /// <summary>Returns one row of ARGB pixels per scan line of the text line.</summary>
    private byte[][] RenderTextLine(int lineHeight)
    {
        // Build the full pixel row buffer (width × lineHeight), all white
        var pixels = new byte[lineHeight][];
        for (int r = 0; r < lineHeight; r++)
        {
            pixels[r] = new byte[_paperWidthDots * 3]; // RGB
            for (int i = 0; i < pixels[r].Length; i++) pixels[r][i] = 0xFF; // white
        }

        if (_pendingSegments.Count == 0)
            return pixels;

        // Measure total text width to compute alignment offset
        int totalTextWidth = 0;
        foreach (var seg in _pendingSegments)
            totalTextWidth += seg.text.Length * BitmapFont.CharWidth * seg.wMul;

        int xOffset = _pendingSegments[0].alignment switch
        {
            TextAlignment.Center => (_paperWidthDots - totalTextWidth) / 2,
            TextAlignment.Right  => _paperWidthDots - totalTextWidth,
            _                    => 0,
        };
        if (xOffset < 0) xOffset = 0;

        int x = xOffset;
        foreach (var (text, bold, underline, _, wMul, hMul) in _pendingSegments)
        {
            int cellW = BitmapFont.CharWidth * wMul;
            int cellH = BitmapFont.CharHeight * hMul;
            int yOffset = lineHeight - cellH; // bottom-align within mixed-size line

            foreach (char c in text)
            {
                var glyph = BitmapFont.GetGlyph(c);

                for (int gy = 0; gy < BitmapFont.CharHeight; gy++)
                {
                    byte rowByte = glyph[gy];
                    for (int gx = 0; gx < BitmapFont.CharWidth; gx++)
                    {
                        bool ink = (rowByte & (0x80 >> gx)) != 0;
                        if (bold && !ink && gx > 0)
                            ink = (rowByte & (0x80 >> (gx - 1))) != 0; // simple bold: repeat 1px right

                        if (!ink) continue;

                        // Scale pixel by wMul × hMul
                        for (int dy = 0; dy < hMul; dy++)
                        {
                            int py = yOffset + gy * hMul + dy;
                            if (py >= lineHeight) continue;
                            byte[] rowPixels = pixels[py];

                            for (int dx = 0; dx < wMul; dx++)
                            {
                                int px = x + gx * wMul + dx;
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
