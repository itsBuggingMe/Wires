using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wires.Core;

namespace Wires;

public class WiresGame : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public WiresGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
        };
        Content.RootDirectory = "Content";
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
            .Add(new ShapeBatch(_graphics.GraphicsDevice, Content))
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
