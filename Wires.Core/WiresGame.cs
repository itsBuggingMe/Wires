using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Paper.Core;
using System;
using Wires.Core;
using Wires.Core.States;
using Wires.States;

namespace Wires.Core;

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
        }
        ;

#if !BLAZORGL
        Window.AllowUserResizing = true;
#endif
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
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

        ScreenManager manager = ScreenManager.Create<CampaignState>(serviceContainer, this);
        serviceContainer.Add(manager);

        Components.Add(manager);
        base.Initialize();
    }
}