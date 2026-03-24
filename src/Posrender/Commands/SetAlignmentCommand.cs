namespace Posrender.Commands;

public enum TextAlignment { Left = 0, Center = 1, Right = 2 }

/// <summary>ESC a — sets text justification.</summary>
public sealed class SetAlignmentCommand : IEscPosCommand
{
    public TextAlignment Alignment { get; }

    public SetAlignmentCommand(TextAlignment alignment) => Alignment = alignment;
}
