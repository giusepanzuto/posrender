namespace Posrender.Commands;

public enum UnderlineMode { None = 0, Single = 1, Double = 2 }

/// <summary>ESC - — sets underline mode.</summary>
public sealed class SetUnderlineCommand : IEscPosCommand
{
    public UnderlineMode Mode { get; }

    public SetUnderlineCommand(UnderlineMode mode) => Mode = mode;
}
