namespace Posrender.Commands;

/// <summary>GS ! / ESC ! — sets character width and height multipliers (1–8).</summary>
public sealed class SetFontSizeCommand : IEscPosCommand
{
    public int WidthMultiplier { get; }
    public int HeightMultiplier { get; }

    public SetFontSizeCommand(int widthMultiplier, int heightMultiplier)
    {
        WidthMultiplier = widthMultiplier;
        HeightMultiplier = heightMultiplier;
    }
}
