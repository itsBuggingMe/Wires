using Microsoft.Xna.Framework;

namespace Wires.Core.UI;

internal class StackPanel : BorderedElement
{
    private readonly bool _isHorizontal;

    public int Padding { get; set; } = Constants.Padding;

    public StackPanel(Vector2 pos, bool horizontal = true) : base(pos, default)
    {
        _isHorizontal = horizontal;
    }

    public override void Draw()
    {
        Vector2 start = new Vector2(Padding);
        foreach (var child in Children)
        {
            child.SetPosition(start);
            start += (child.Size + new Vector2(Padding)) * (_isHorizontal ? Vector2.UnitY : Vector2.UnitX);
        }

        base.Draw();
    }
}