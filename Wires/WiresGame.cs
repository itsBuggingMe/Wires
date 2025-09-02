using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;

namespace Wires;

public class WiresGame : Game
{
    GraphicsDeviceManager graphics;
    SpriteBatch spriteBatch;

    public WiresGame()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        base.Initialize();

    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);

    }

    protected override void Update(GameTime gameTime)
    {
        MouseState mouseState = Mouse.GetState();
        KeyboardState keyboardState = Keyboard.GetState();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        base.Draw(gameTime);
    }
}
