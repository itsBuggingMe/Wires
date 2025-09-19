using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Paper.Core;

namespace Wires.Core;

public class Graphics : GraphicsBase
{
    public ShapeBatch ShapeBatch { get; private set; }
    public SpriteBatch SpriteBatchText { get; private set; }
    public SpriteFont Font => Fonts.Main;

    public Graphics(GraphicsDeviceManager graphicsDeviceManager, ContentManager content, Game game)
        : base(graphicsDeviceManager, content, game)
    {
        ShapeBatch = new ShapeBatch(GraphicsDevice, content);
        SpriteBatchText = new(GraphicsDevice);
    }

    public void DrawStringCentered(string text, Vector2 position, float scale = 1, Color? color = default, float rotation = default)
    {
        Vector2 size = Font.MeasureString(text) * scale;
        SpriteBatchText.DrawString(Font, text, position
#if !BLAZORGL
            + Vector2.UnitX * 1.4f
#endif
            , color ?? Color.White, rotation, size * 0.5f, scale, default, default);
    }

    public void DrawString(string text, Vector2 position, Vector2 alignment = default, float scale = 1, Color? color = default, float rotation = default)
    {
        Vector2 size = Font.MeasureString(text) * scale;
        SpriteBatchText.DrawString(Font, text, position, color ?? Color.White, rotation, size * alignment, scale, default, default);
    }

    public void StartBatches()
    {
        ShapeBatch.Begin();
        SpriteBatch.Begin();
        SpriteBatchText.Begin();
    }

    public void EndBatches()
    {
        ShapeBatch.End();
        SpriteBatch.End();
        SpriteBatchText.End();
    }
}