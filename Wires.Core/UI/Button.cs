using Microsoft.Xna.Framework;
using Paper.Core;
using Paper.Core.UI;
using System;

namespace Wires.Core.UI;

internal class Button : BorderedElement
{
    public static readonly Action<Button> Pancake = b =>
    {
        const int Padding = 14;

        Vector2 pos = b.Position + new Vector2(Padding);
        Vector2 size = b.Size - 2 * new Vector2(Padding);

        float third = size.Y / 2f;
        for (int i = 0; i < 3; i++)
        {
            Vector2 left = new(pos.X, pos.Y + i * third);
            b.Graphics.ShapeBatch.FillLine(left, new Vector2(pos.X + size.X, left.Y), 3f, Constants.UILight * b.TactileFeedback);
        }
    };

    private readonly Action<Button> _draw;
    public Action<Point>? Clicked { get; set; }
    public Action<Point>? Drag { get; set; }
    public Action<Point>? DragRelease { get; set; }

    public float TactileFeedback => Bounds.Contains(InputHelper.MouseLocation) ?
        MouseButton.Left.Down() ?
            1.3f :
            1.1f :
            1f;

    private bool _isDragged;

    public Button(UIVector2 pos, UIVector2 size, Action<Button> drawAction) : base(pos, size)
    {
        _draw = drawAction;
    }

    public override void Update()
    {
        if (MouseButton.Left.FallingEdge() && Bounds.Contains(InputHelper.MouseLocation))
            Clicked?.Invoke(InputHelper.MouseLocation);

        if (MouseButton.Left.RisingEdge() && Bounds.Contains(InputHelper.MouseLocation))
        {
            _isDragged = true;
        }

        if (!MouseButton.Left.Down() && _isDragged)
        {
            _isDragged = false;
            DragRelease?.Invoke(InputHelper.MouseLocation);
        }

        if (_isDragged)
            Drag?.Invoke(InputHelper.MouseLocation);

        base.Update();
    }

    public override void Draw()
    {
        ColorMultipler = TactileFeedback;
        base.Draw();
        _draw(this);
    }
}
