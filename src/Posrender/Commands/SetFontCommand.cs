namespace Posrender.Commands;

public enum PrinterFont { A = 0, B = 1 }

/// <summary>ESC M — selects character font (A or B).</summary>
public sealed class SetFontCommand : IEscPosCommand
{
    public PrinterFont Font { get; }

    public SetFontCommand(PrinterFont font) => Font = font;
}
