using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections;
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
    private Point? _dragStart;

    private IEnumerator _calcSteps;

    public MainSimulation(Camera2D camera, ShapeBatch sb, Graphics graphics)
    {
        _graphics = graphics;
        _camera = camera;
        _sb = sb;
        _sim = new Simulation();

        _sim.Place(Blueprint.On, new Point(1, 4));
        _sim.Place(Blueprint.Off, new Point(1, 8));
        _sim.Place(Blueprint.Output, new Point(16, 10));
    }

    public void Update(Time gameTime)
    {
        if(_calcSteps is not null)
        {
            if(InputHelper.FallingEdge(Keys.Q) && !_calcSteps.MoveNext())
            {
                _calcSteps = null;
            }
        }
        else if(InputHelper.RisingEdge(Keys.R))
        {
            _calcSteps = _sim.Step().GetEnumerator();
        }
        else
        {
            SimInteract();
        }

        UpdateCamera();
    }

    private void SimInteract()
    {
        Point tileOver = GetTileOver();
        if (InputHelper.RisingEdge(Keys.E))
        {
            _sim.Place(Blueprint.Transisstor, tileOver);
        }
        if (InputHelper.RisingEdge(Keys.N))
        {
            _sim.Place(Blueprint.Not, tileOver);
        }
        if (InputHelper.RisingEdge(MouseButton.Left) && _sim.InRange(tileOver) && 
            (_sim[tileOver].Kind is TileKind.Output or TileKind.Input || _sim.IdOfWireAt(tileOver) is int))
        {
            _dragStart = tileOver;
        }

        if (InputHelper.FallingEdge(MouseButton.Left)
            && _dragStart is Point dragStart
            && _sim.InRange(tileOver)
            //&& _sim[tileOver].Kind is TileKind.Output or TileKind.Input
            && tileOver != dragStart)
        {
            _sim.CreateWire(new Wire(dragStart, tileOver));
            _calcSteps = _sim.Step().GetEnumerator();
            _dragStart = null;
        }

        if (InputHelper.FallingEdge(MouseButton.Right) && _sim.IdOfWireAt(tileOver) is int id)
        {
            _sim.DestroyWire(id);
            _calcSteps = _sim.Step().GetEnumerator();
        }

        if(_calcSteps is not null && !InputHelper.Down(Keys.Space))
        {
            while (_calcSteps.MoveNext());
            _calcSteps = null;
        }
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);

        _sb.Begin(_camera.View, _camera.Projection);
        DrawGrid(new Vector2(-Step / 2), 100);
        Vector2 scale = new Vector2(Step);
        _sim.Draw(_sb, scale, 10);
        _sb.End();
    }

    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-Step / 2)) / new Vector2(Step)).ToPoint();

    private void UpdateCamera()
    {
        const float ScrollScaleM = 1.2f;
        if (InputHelper.DeltaScroll != 0)
            _camera.Scale *= InputHelper.DeltaScroll > 0 ? ScrollScaleM : 1 / ScrollScaleM;

        if (InputHelper.Down(MouseButton.Left) && _dragStart is null)
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
