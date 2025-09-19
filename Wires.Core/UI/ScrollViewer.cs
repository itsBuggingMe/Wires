using Microsoft.Xna.Framework;

namespace Wires.Core.UI;

internal class ScrollViewer : BorderedElement
{
    private readonly bool _isHorizontal;

    public ScrollViewer(Vector2 pos, Vector2 size, bool horizontal = true) : base(pos, size)
    {
        _isHorizontal = horizontal;
    }

    public override void Draw()
    {
        Vector2 start = new Vector2(Constants.Padding);
        foreach(var child in Children)
        {
            child.SetPosition(start);
            start += child.Size * (_isHorizontal ? Vector2.UnitY : Vector2.UnitX);
        }


        Graphics.EndBatches();

        Graphics.GraphicsDevice.ScissorRectangle = Bounds;
        Graphics.StartBatches();
        
        base.Draw();

        Graphics.EndBatches();
        Graphics.StartBatches();
    }
}
