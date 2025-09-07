using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wires.Core;
using Wires.States;
using Apos.Shapes;

namespace Wires;

public class WiresGame : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public WiresGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
#if !BLAZORGL
            PreferredBackBufferHeight = 720,
            PreferredBackBufferWidth = 1280,
#endif
        };
            Content.RootDirectory = "Content";
        IsMouseVisible = true;

    }

    protected override void Initialize()
    {
        Graphics graphics = new(_graphics, Content);

        ServiceContainer serviceContainer = new();
        serviceContainer
            .Add(serviceContainer)

            .Add(Content)

            .Add(_graphics)
            .Add(_graphics.GraphicsDevice)

            .Add(new Camera2D(_graphics.GraphicsDevice))
            .Add(new Time())

            .Add(graphics)
            .Add(graphics.SpriteBatch)
            .Add(graphics.WhitePixel)

            .Add(this)
            ;

        ScreenManager manager = ScreenManager.Create<MainSimulation>(serviceContainer, this);

        Components.Add(manager);
        base.Initialize();
    }
}
