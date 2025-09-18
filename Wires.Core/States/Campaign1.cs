using Paper.Core;
using Paper.Core.UI;
using Wires.Core.UI;
using Microsoft.Xna.Framework;

namespace Wires.Core.States;

internal class Campaign1 : IScreen
{
    private readonly RootUI<Graphics> _root;
    private readonly Graphics _graphics;
    public Campaign1(Graphics graphics)
    {
        _graphics = graphics;

        _root = new RootUI<Graphics>(graphics.Game, graphics, 1920, 1080)
        {
            Children = 
            [
                new BorderedElement(Vector2.One * 200, Vector2.One * 100),
            ],
        };
    }

    public void Update(Time gameTime)
    {
        _root.Update();
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);
        _graphics.ShapeBatch.Begin();
        _graphics.SpriteBatch.Begin();
        _graphics.SpriteBatchText.Begin();
        _root.Draw();
        _graphics.ShapeBatch.End();
        _graphics.SpriteBatch.End();
        _graphics.SpriteBatchText.End();
    }

    public void OnEnter(IScreen previous, object? args) { }
    public object? OnExit() => null;
}
