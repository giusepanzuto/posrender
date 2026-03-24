namespace Posrender.Commands;

/// <summary>LF / ESC d — advance paper by one or more lines.</summary>
public sealed class LineFeedCommand : IEscPosCommand
{
    public int Lines { get; }

    public LineFeedCommand(int lines = 1) => Lines = lines;
}
