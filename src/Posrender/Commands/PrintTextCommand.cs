namespace Posrender.Commands;

/// <summary>Printable text content accumulated from raw bytes.</summary>
public sealed class PrintTextCommand : IEscPosCommand
{
    public string Text { get; }

    public PrintTextCommand(string text) => Text = text;
}
