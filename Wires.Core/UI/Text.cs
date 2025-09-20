using Microsoft.Xna.Framework;
using Paper.Core.UI;
using System;
namespace Wires.Core.UI;

internal class Text : UIBase<Graphics>
{
    public string? Content { get; set; }
    public Func<string?>? ContentSource { get; set; }
    public Color Color { get; set; } = Color.White;
    public Vector2 Scale { get; set; } = Vector2.One;
    public Text(UIVector2 xy, string? text = null, Func<string?>? contentSource = null) : base(xy)
    {
        Content = text;
        ContentSource = contentSource;
    }

    public override void Draw()
    {
        base.Draw();
        if((Content ?? ContentSource?.Invoke()) is string @string)
        {
            var size = Graphics.Font.MeasureString(@string) * Scale;
            SetSize(size);
            Rectangle bounds = Bounds;
            Graphics.SpriteBatchText.DrawString(Graphics.Font, @string, bounds.Location.ToVector2(), Color, 0, default, Scale, default, 0);
            //Graphics.ShapeBatch.DrawRectangle(bounds.Location.ToVector2(), bounds.Size.ToVector2(), Color.Red, Color.Red);
        }
    }
}
