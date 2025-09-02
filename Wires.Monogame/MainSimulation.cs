using Apos.Shapes;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Xml;
using Wires.Core;
using Wires.Sim;

namespace Wires;

public class MainSimulation : IScreen
{
    private Camera2D _camera;
    private ShapeBatch _sb;
    private Graphics _graphics;
    private Simulation _sim;

    public MainSimulation(Camera2D camera, ShapeBatch sb, Graphics graphics)
    {
        _graphics = graphics;
        _camera = camera;
        _sb = sb;
        _sim = new Simulation();
    }

    public void Update(Time gameTime)
    {
        UpdateCamera();

    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);

        _sb.Begin(_camera.View, _camera.Projection);
        DrawGrid(new Vector2(-Step / 2), 100);
        Vector2 scale = new Vector2(Step);
        foreach(ref var wire in _sim.Wires)
        {
            if (!wire.Exists)
                continue;
            Color color;
            Color outline;
            if (wire.IsInactive)
            {
                outline = Color.Gray;
                color = Color.DarkGray;
            }
            else
            {
                (color, outline) = wire.Value == default ?
                    (Color.Red, Color.DarkRed):
                    (Color.Green, Color.DarkGreen);
            }
            _sb.DrawLine(wire.From.ToVector2() * scale, wire.To.ToVector2() * scale, 15, color, outline, 4);
        }
        _sb.End();
    }


    private void UpdateCamera()
    {
        const float ScrollScaleM = 1.2f;
        if (InputHelper.DeltaScroll != 0)
            _camera.Scale *= InputHelper.DeltaScroll > 0 ? ScrollScaleM : 1 / ScrollScaleM;

        if (InputHelper.Down(MouseButton.Left))
            _camera.Position += (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2());
    }

    const float Step = 50f;
    const float LineWidth = 1f;

    private void DrawGrid(Vector2 origin, int n)
    {
        float gridSize = n * Step;

        for (int i = 0; i <= n; i++)
        {
            float x = origin.X + i * Step;
            _sb.FillLine(
                new Vector2(x, origin.Y),
                new Vector2(x, origin.Y + gridSize),
                LineWidth,
                Constants.Accent
            );
        }

        for (int j = 0; j <= n; j++)
        {
            float y = origin.Y + j * Step;
            _sb.FillLine(
                new Vector2(origin.X, y),
                new Vector2(origin.X + gridSize, y),
                LineWidth,
                Constants.Accent
            );
        }
    }


    public void OnEnter(IScreen previous, object args) { }
    public object OnExit() => null;
}
