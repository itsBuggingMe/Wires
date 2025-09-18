using Paper.Core.UI;
using Microsoft.Xna.Framework;

namespace Wires.Core.UI;

internal class BorderedElement : UIBase<Graphics>
{
    public BorderedElement(Vector2 pos, Vector2 size) : base(new(pos), new(size))
    {

    }

    public override void Draw()
    {
        Graphics.ShapeBatch.DrawRectangle(Position, Size, Constants.UILight, Constants.UIDark, 4, 4);
        base.Draw();
    }
}
