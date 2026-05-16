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
    public float? WrapLength { get; set; }
    public bool WrapToViewport { get; set; }
    public int ViewportWrapMargin { get; set; } = Constants.Padding;

    public StackPanel(Vector2 pos, UIVector2 shortAxis, bool horizontal = true) : base(pos, shortAxis)
    {
        _isHorizontal = horizontal;
    }

    public override void Draw()
    {
        if (_isHorizontal && (WrapLength is not null || WrapToViewport))
        {
            float wrapLength = WrapLength ?? Math.Max(
                Padding * 2,
                Graphics.GraphicsDevice.Viewport.Width - Position.X - ViewportWrapMargin);
            DrawWrappedHorizontal(wrapLength);
            return;
        }

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

    private void DrawWrappedHorizontal(float wrapLength)
    {
        float rowWidth = Padding;
        float rowHeight = 0;
        float width = 0;
        float height = Padding;

        foreach (var child in Children)
        {
            Vector2 size = child.Size;

            if (rowWidth > Padding && rowWidth + size.X + Padding > wrapLength)
            {
                width = Math.Max(width, rowWidth);
                height += rowHeight + Padding;
                rowWidth = Padding;
                rowHeight = 0;
            }

            child.SetPosition(new Vector2(rowWidth, height));
            rowWidth += size.X + Padding;
            rowHeight = Math.Max(rowHeight, size.Y);
        }

        width = Math.Max(width, rowWidth);
        height += rowHeight + Padding;

        SetSize(new Vector2(width, height));

        base.Draw();
    }
}
