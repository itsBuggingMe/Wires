using Paper.Core.UI;
using Microsoft.Xna.Framework;

namespace Wires.Core.UI;

internal class BorderedElement : UIBase<Graphics>
{
    public BorderedElement(UIVector2 pos, UIVector2 size) : base(pos, size)
    {

    }

    protected float ColorMultipler = 1;

    public override void Draw()
    {
        Rectangle bounds = Bounds;
        Graphics.ShapeBatch.DrawRectangle(bounds.Location.ToVector2(), bounds.Size.ToVector2(), Constants.UIDark * ColorMultipler, Constants.UILight * ColorMultipler, 4, 4);
        base.Draw();
    }
}
