namespace Posrender;

/// <summary>
/// Options for rendering an ESC/POS byte stream to a PNG image.
/// </summary>
public class PosRenderOptions
{
    /// <summary>
    /// Paper width in dots. Default is 576, which corresponds to the 72 mm printable
    /// area of a standard Epson 80 mm paper-roll printer at 203 DPI.
    /// Use <see cref="FromMillimeters"/> to compute the value from physical dimensions.
    /// </summary>
    public int PaperWidthDots { get; set; } = 576;

    /// <summary>Printer DPI. Default is 203.</summary>
    public int Dpi { get; set; } = 203;

    /// <summary>
    /// Creates a <see cref="PosRenderOptions"/> from a physical paper width in millimeters.
    /// </summary>
    /// <param name="widthMm">Paper width in millimeters (e.g. 58, 80, 112).</param>
    /// <param name="dpi">Printer resolution in dots per inch. Default is 203.</param>
    public static PosRenderOptions FromMillimeters(int widthMm, int dpi = 203) =>
        new() { Dpi = dpi, PaperWidthDots = (int)Math.Round(widthMm * dpi / 25.4) };
}
