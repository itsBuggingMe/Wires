using Microsoft.Xna.Framework;
using System.Reflection;
using Paper.Core.UI;
using System;
using ImGuiNET;

namespace Wires.Core.UI;

internal class StackPanel : BorderedElement
{
    private readonly bool _isHorizontal;

    public int Padding { get; set; } = 8;

    public StackPanel(Vector2 pos, UIVector2 shortAxis, bool horizontal = true) : base(pos, shortAxis)
    {
        _isHorizontal = horizontal;
    }

    public override void Draw()
    {
        float max = default;
        float len = default;

        Vector2 start = new Vector2(Padding);

        int last = Children.Count - 1;
        int index = 0;

        foreach (var child in Children)
        {
            child.SetPosition(start);
            Vector2 size = child.Size;
            start += (size + new Vector2(Padding)) * (_isHorizontal ? Vector2.UnitX : Vector2.UnitY);

            max = Math.Max(max, _isHorizontal ?
                size.Y :
                size.X);

            if(last == index)
            {
                len = _isHorizontal ?
                    start.X + size.X - Padding - Position.X :
                    start.Y + size.Y - Padding - Position.Y;
            }

            index++;
        }

        max += 2 * Padding;

        SetSize(_isHorizontal ? new(len, max) : new(max, len));

        base.Draw();
    }
}