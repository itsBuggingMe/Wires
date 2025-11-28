using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Paper.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text;

namespace Wires.Core.Sim;

internal class SimInteraction
{
    private ComponentEntry? _activeDragDrop;
    private int _rotation;
    private int? _draggedComponentId;
    private Point? _wireDragStart;
    private bool _currentPlacedIsBundle = true;
    private Point _wireDragCurrent;

    private readonly Camera2D _camera;
    private readonly Graphics _graphics;

    public ShortCircuitDescription? ShortCircuitDescription { get; private set; }

    public ComponentEntry? ActiveEntry
    { 
        get;
        set
        {
            Reset();
            field = value;
            if(value is { Custom: { } sim })
                _camera.Position = new Vector2(Constants.Scale * sim.Width - Constants.Scale, Constants.Scale * sim.Height - Constants.Scale) * -0.5f;
        }
    }

    public Simulation? ActiveSim => ActiveEntry?.Custom;

    public SimInteraction(Graphics graphics, GlobalStateTable stateTable)
    {
        _globalStateTable = stateTable;
        _camera = graphics.Camera;
        _graphics = graphics;
    }

    public void UpdateSim()
    {
        if (ActiveSim is not Simulation sim)
            return;
        UpdateCore();

        if(!MouseButton.Left.Down() && !MouseButton.Right.Down())
        {
            _wireDragStart = default;
            _draggedComponentId = default;
            _activeDragDrop = default;
        }

        void UpdateCore()
        {
            Point tileOver = GetTileOver();

            if (!sim.InRange(tileOver))
                return;


            // switch

            if (InputHelper.FallingEdge(MouseButton.Left))
            {
                foreach (var component in sim.Components)
                {
                    if (component.Position == tileOver && component is { Blueprint.Descriptor: Blueprint.IntrinsicBlueprint.Switch })
                    {
                        component.Blueprint.SwitchValue = component.Blueprint.SwitchValue.On ? PowerState.OffState : PowerState.OnState;
                        Step();
                    }
                }
            }

            // placing components
            if (_activeDragDrop is not null && InputHelper.FallingEdge(MouseButton.Left))
            {
                if(_activeDragDrop.Blueprint.Custom != sim)
                {
                    sim.Place(_activeDragDrop.Blueprint, GetTileOver(), _rotation);
                    Step();
                }
                return;
            }

            if (_activeDragDrop is not null && InputHelper.Down(MouseButton.Right))
            {
                sim.Place(_activeDragDrop.Blueprint, GetTileOver(), _rotation);
                Step();
                return;
            }

            if (_activeDragDrop is not null && InputHelper.FallingEdge(Keys.Space))
            {
                _rotation++;
                return;
            }

            // placing wires
            if ((InputHelper.RisingEdge(MouseButton.Left) || InputHelper.RisingEdge(MouseButton.Right)) && (sim[tileOver] is { Kind: TileKind.Input or TileKind.Output } || sim.HasWiresAt(tileOver)))
            {
                _wireDragStart = tileOver;
                _wireDragCurrent = tileOver;
                _currentPlacedIsBundle = InputHelper.RisingEdge(MouseButton.Right);
                return;
            }

            if (_wireDragStart is not null && sim[tileOver] is not { Kind: TileKind.Component })
            {
                _wireDragCurrent = tileOver;
            }

            if (_wireDragStart is not null && !(_currentPlacedIsBundle ? InputHelper.Down(MouseButton.Right) : InputHelper.Down(MouseButton.Left)) && _wireDragStart != _wireDragCurrent)
            {
                sim.CreateWire(new Wire { A = _wireDragStart.Value, B = _wireDragCurrent, WireKind = _currentPlacedIsBundle });
                _wireDragStart = null;
                Step();
                return;
            }

            // moving components
            if (MouseButton.Left.RisingEdge() &&
                (sim[tileOver].Kind is TileKind.Output or TileKind.Input or TileKind.Component))
            {
                _draggedComponentId = sim[tileOver].ComponentId;
                return;
            }

            if (_draggedComponentId is int draggedComponentId && !sim.HasWiresAt(tileOver))
            {
                sim.MoveComponent(draggedComponentId, tileOver);
                Step();
                if (!InputHelper.Down(MouseButton.Left))
                {
                    _draggedComponentId = null;
                }
                return;
            }

            if (MouseButton.Right.FallingEdge() && _activeDragDrop is null)
            {
                if (sim.IdOfWireAt(tileOver) is int id)
                {
                    sim.DestroyWire(id);
                    Step();
                }
                else if (sim[tileOver].Kind is not TileKind.Nothing)
                {
                    sim.DestroyComponent(tileOver);
                    Step();
                }
                return;
            }
        }
    }

    private GlobalStateTable _globalStateTable;
    public GlobalStateTable State => _globalStateTable;

    public void Step()
    {
        _globalStateTable.Reset();
        ShortCircuitDescription = ActiveEntry?.Blueprint.Reset(_globalStateTable, 92821 /*this is root!*/);
    }

    public void UpdateCamera()
    {
        const float ScrollScaleM = 1.2f;
        if (InputHelper.DeltaScroll != 0)
            _camera.Scale *= InputHelper.DeltaScroll > 0 ? ScrollScaleM : 1 / ScrollScaleM;
        if (Keys.OemPlus.RisingEdge())
            _camera.Scale *= ScrollScaleM;
        if (Keys.OemMinus.RisingEdge())
            _camera.Scale /= ScrollScaleM;

        if (MouseButton.Left.Down() && _draggedComponentId is null && _wireDragStart is null && _activeDragDrop is null)
            _camera.Position -= (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2()) / _camera.Scale;
    }

    public void Reset()
    {
        _wireDragStart = default;
        _draggedComponentId = default;
        _activeDragDrop = default;
        _rotation = default;
    }

    public void BeginPlaceComponent(ComponentEntry componentEntry)
    {
        _activeDragDrop = componentEntry;
    }

    public void Draw()
    {
        if (ActiveSim is null)
            return;

        if (_activeDragDrop is not null)
        {
            _activeDragDrop.Blueprint.Draw(_graphics, null, GetTileOver(), Constants.Scale, Constants.WireRad, false, 0, rotationOverride: _rotation);
        }

        if (_wireDragStart is Point dragStart
            && _wireDragCurrent != dragStart)
        {
            var colors = _currentPlacedIsBundle ?
                Constants.BundleWireColor :
                Constants.GetWireColor(PowerState.OffState);
            ActiveSim.DrawWire(_graphics, Constants.Scale, new Wire(dragStart, _wireDragCurrent), colors.Color * 0.5f, colors.Output * 0.5f);
        }
    }
    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-50 / 2)) / new Vector2(50)).ToPoint();
}
