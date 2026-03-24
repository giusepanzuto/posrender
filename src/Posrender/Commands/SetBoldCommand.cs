namespace Posrender.Commands;

/// <summary>ESC E — enables or disables bold printing.</summary>
public sealed class SetBoldCommand : IEscPosCommand
{
    public bool On { get; }

    public SetBoldCommand(bool on) => On = on;
}
