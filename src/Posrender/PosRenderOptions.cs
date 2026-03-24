namespace Posrender;

/// <summary>
/// Options for rendering an ESC/POS byte stream to a PNG image.
/// </summary>
public class PosRenderOptions
{
    /// <summary>Paper width in dots. Default is 576 (80 mm at 203 DPI).</summary>
    public int PaperWidthDots { get; set; } = 576;

    /// <summary>Printer DPI. Default is 203.</summary>
    public int Dpi { get; set; } = 203;
}
