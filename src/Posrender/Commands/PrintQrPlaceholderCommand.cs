namespace Posrender.Commands;

/// <summary>Represents a QR code (GS ( k print) as a square placeholder of a given dot size.</summary>
public sealed class PrintQrPlaceholderCommand : IEscPosCommand
{
    /// <summary>Side length of the QR code placeholder in dots.</summary>
    public int SizeDots { get; }

    public PrintQrPlaceholderCommand(int sizeDots)
    {
        SizeDots = sizeDots;
    }
}
