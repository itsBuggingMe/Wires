using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Apos.Shapes;

namespace Wires.Core;

public class Graphics
{
    public SpriteBatch SpriteBatch { get; private set; }
    public SpriteBatch SpriteBatchText { get; private set; }
    public Texture2D WhitePixel { get; private set; }
    public GraphicsDevice GraphicsDevice { get; private set; }
    public GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
    public ContentManager Content { get; private set; }
    public SpriteFont Font { get; private set; }
    public ShapeBatch ShapeBatch { get; private set; }

    public Graphics(GraphicsDeviceManager graphicsDeviceManager, ContentManager content)
    {
        Content = content;
        GraphicsDeviceManager = graphicsDeviceManager;
        GraphicsDevice = graphicsDeviceManager.GraphicsDevice;
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        SpriteBatchText = new SpriteBatch(GraphicsDevice);
        ShapeBatch = new ShapeBatch(GraphicsDevice, content);
        WhitePixel = new Texture2D(GraphicsDevice, 1, 1);
        WhitePixel.SetData([Color.White]);
        Font = Content.Load<SpriteFont>("MainFont");
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

    public void DrawRectangle(Rectangle rectangle, Color color)
    {
        SpriteBatch.Draw(WhitePixel, rectangle, color);
    }
}