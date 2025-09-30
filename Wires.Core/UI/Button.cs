using Microsoft.Xna.Framework;
using Paper.Core;
using Paper.Core.UI;
using System;
using System.Diagnostics;
using System.Linq;

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

    public static readonly Action<Button> Plus = b =>
    {
        const int Padding = 14;

        Vector2 pos = b.Position + new Vector2(Padding);
        Vector2 size = b.Size - 2 * new Vector2(Padding);
        Vector2 sizeHalf = size * 0.5f;

        b.Graphics.ShapeBatch.FillLine(new Vector2(pos.X + sizeHalf.X, pos.Y), new Vector2(pos.X + sizeHalf.X, pos.Y + size.Y), 3f, Constants.UILight * b.TactileFeedback);
        b.Graphics.ShapeBatch.FillLine(new Vector2(pos.X, pos.Y + sizeHalf.Y), new Vector2(pos.X + size.X, pos.Y + sizeHalf.Y), 3f, Constants.UILight * b.TactileFeedback);
    };


    public static readonly Action<Button> None = b => { };

    private readonly Action<Button> _draw;
    public Action<Point>? Clicked { get; set; }
    public Action<Point>? RisingEdge { get; set; }
    public Action<Point>? Drag { get; set; }
    public Action<Point>? DragRelease { get; set; }
    public Action<Point>? Hover { get; set; }

    public Text? Text 
    {
        get => field; 
        set
        {
            if(field is not null)
                RemoveChild(field);
            if(value is not null)
                AddChild(value);
            field = value;
        }
    }

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

    public override bool Update()
    {
        if (!Visible)
        {
            return base.Update();
        }

        if (MouseButton.Left.FallingEdge() && Bounds.Contains(InputHelper.MouseLocation))
            Clicked?.Invoke(InputHelper.MouseLocation);

        if (MouseButton.Left.RisingEdge() && Bounds.Contains(InputHelper.MouseLocation))
        {
            _isDragged = true;
            RisingEdge?.Invoke(InputHelper.MouseLocation);
        }

        if(Bounds.Contains(InputHelper.MouseLocation))
        {
            Hover?.Invoke(InputHelper.MouseLocation);
        }

        if (!MouseButton.Left.Down() && _isDragged)
        {
            _isDragged = false;
            DragRelease?.Invoke(InputHelper.MouseLocation);
        }

        if (_isDragged)
            Drag?.Invoke(InputHelper.MouseLocation);

        return base.Update() || Bounds.Contains(InputHelper.MouseLocation);
    }

    public override void Draw()
    {
        ColorMultipler = TactileFeedback;
        base.Draw();
        _draw(this);
    }
}
