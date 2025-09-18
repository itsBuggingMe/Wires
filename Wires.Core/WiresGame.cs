using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wires.Core;
using Paper.Core;
using Wires.States;
using MonoGameGum.Forms;
using MonoGameGum;
using Gum.Forms.DefaultVisuals;

namespace Wires.Core;

public class WiresGame : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public WiresGame()
    {
        _graphics = new GraphicsDeviceManager(this)
#if !BLAZORGL
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredBackBufferHeight = 720,
            PreferredBackBufferWidth = 1280,
        }
#endif
        ;
        _graphics.PreparingDeviceSettings += (o, e) =>
        {
            _graphics.PreferMultiSampling = true;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 8;
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        GumService.Default.Initialize(this, DefaultVisualsVersion.V2);

        Color dark = new Color(33, 24, 24);
        Color light = new Color(92, 62, 62);

        Styling.ActiveStyle.Colors.PrimaryLight = light * 1.2f;
        Styling.ActiveStyle.Colors.Primary = light;
        Styling.ActiveStyle.Colors.PrimaryDark = dark;
        Styling.ActiveStyle.Colors.DarkGray = dark;
        Styling.ActiveStyle.Colors.LightGray= light;

        Graphics graphics = new(_graphics, Content, this);

        ServiceContainer serviceContainer = new();
        serviceContainer
            .Add(serviceContainer)

            .Add(Content)

            .Add(_graphics)
            .Add(_graphics.GraphicsDevice)

            .Add(new Time())

            .Add(graphics)
            .Add(graphics.Camera)
            .Add(graphics.SpriteBatch)
            .Add(graphics.WhitePixel)

            .Add(this)
            ;

        ScreenManager manager = ScreenManager.Create<MainSimulation>(serviceContainer, this);
        serviceContainer.Add(manager);

        Components.Add(manager);
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        GumService.Default.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        GumService.Default.Draw();
    }
}
