using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Wires.Core;

public class Graphics
{
    public SpriteBatch SpriteBatch { get; private set; }
    public Texture2D WhitePixel { get; private set; }
    public GraphicsDevice GraphicsDevice { get; private set; }
    public GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
    public ContentManager Content { get; private set; }
    public Graphics(GraphicsDeviceManager graphicsDeviceManager, ContentManager content)
    {
        Content = content;
        GraphicsDeviceManager = graphicsDeviceManager;
        GraphicsDevice = graphicsDeviceManager.GraphicsDevice;
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        WhitePixel = new Texture2D(GraphicsDevice, 1, 1);
        WhitePixel.SetData([Color.White]);
    }

    public void DrawRectangle(Rectangle rectangle, Color color)
    {
        SpriteBatch.Draw(WhitePixel, rectangle, color);
    }
}