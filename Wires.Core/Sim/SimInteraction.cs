using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Paper.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
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
    private GroupSelection? _groupSelection;

    private readonly Camera2D _camera;
    private readonly Graphics _graphics;

    private Rectangle? _selectRectangle;
    private Point? _selectionDragPrev;
    private SelectionCopyData? _selectionCopied;

    public ErrDescription? ErrDescription { get; private set; }

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

            // switch

            if (InputHelper.FallingEdge(MouseButton.Left))
            {
                foreach (var component in sim.Components)
                {
                    if (component.Position == tileOver && component is { Blueprint.Descriptor: Blueprint.IntrinsicBlueprint.Switch })
                    {
                        component.Blueprint.SwitchValue = component.Blueprint.SwitchValue.On ? PowerState.OffState : PowerState.OnState;
                        Step();
                        return;
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

            // moving selections
            // handle before wires
            if (_groupSelection is { } selection && InputHelper.RisingEdge(MouseButton.Left) && _selectionDragPrev is null)
            {
                foreach(var componentId in selection.Components)
                {
                    ref Component component = ref sim.GetComponent(componentId);
                    foreach(var offset in component.Blueprint.Display)
                    {
                        if((component.Position + offset.Offset) == tileOver)
                        {
                            _selectionDragPrev = tileOver;
                            return;
                        }
                    }
                }

                foreach (var (wireId, side) in selection.WireNodes)
                {
                    ref Wire w = ref ActiveSim.Wire(wireId);
                    Point pos = side ? w.A : w.B;

                    if(pos == tileOver)
                    {
                        _selectionDragPrev = tileOver;
                        return;
                    }
                }
            }

            if(_selectionDragPrev is Point p && _groupSelection is GroupSelection se)
            {
                if (!MouseButton.Left.Down())
                {
                    _selectionDragPrev = null;
                    return;
                }

                Point delta = tileOver - p;
                _selectionDragPrev = tileOver;
                if(delta != default)
                    sim.MoveMany(se.Components, se.WireNodes, delta);

                return;
            }

            // placing wires
            if ((InputHelper.RisingEdge(MouseButton.Left) || InputHelper.RisingEdge(MouseButton.Right)) && sim.InRange(tileOver) && (sim[tileOver] is { Kind: TileKind.Input or TileKind.Output } || sim.HasWiresAt(tileOver)))
            {
                _wireDragStart = tileOver;
                _wireDragCurrent = tileOver;
                _currentPlacedIsBundle = InputHelper.RisingEdge(MouseButton.Right);
                return;
            }

            if (_wireDragStart is not null && (!sim.InRange(tileOver) || sim[tileOver] is not { Kind: TileKind.Component }))
            {
                _wireDragCurrent = tileOver;
            }

            if (_wireDragStart is not null && !(_currentPlacedIsBundle ? InputHelper.Down(MouseButton.Right) : InputHelper.Down(MouseButton.Left)) && _wireDragStart != _wireDragCurrent)
            {
                if (!sim.InRange(_wireDragStart.Value) || !sim.InRange(_wireDragCurrent))
                    return;

                sim.CreateWire(new Wire { A = _wireDragStart.Value, B = _wireDragCurrent, WireKind = _currentPlacedIsBundle });
                _wireDragStart = null;
                Step();
                return;
            }

            // moving components
            if (MouseButton.Left.RisingEdge() && sim.InRange(tileOver) &&
                (sim[tileOver].Kind is TileKind.Output or TileKind.Input or TileKind.Component))
            {
                _draggedComponentId = sim[tileOver].ComponentId;
                return;
            }

            if (_draggedComponentId is int draggedComponentId && sim.InRange(tileOver) && !sim.HasWiresAt(tileOver))
            {
                sim.MoveComponent(draggedComponentId, tileOver);
                Step();
                if (!InputHelper.Down(MouseButton.Left))
                {
                    _draggedComponentId = null;
                }
                return;
            }

            if (MouseButton.Right.FallingEdge() && _activeDragDrop is null && sim.InRange(tileOver))
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

            if (Keys.LeftShift.Down() && MouseButton.Left.RisingEdge() && _selectRectangle is null)
            {
                _selectRectangle = new Rectangle(_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()).ToPoint(), default);
                return;
            }
            if (_selectRectangle is not null)
            {
                var ploc = _selectRectangle.Value.Location;
                _selectRectangle = new Rectangle(ploc, _camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()).ToPoint() - ploc);

                if (!MouseButton.Left.Down())
                {
                    Rectangle bounds = NormalizeRect(_selectRectangle.Value);
                    bounds.Location /= new Point(Constants.Scale);
                    bounds.Size /= new Point(Constants.Scale);

                    _selectRectangle = null;
                    _groupSelection = new GroupSelection();

                    foreach (var id in sim.ComponentIds)
                    {
                        ref Component component = ref sim.GetComponent(id);
                        if(bounds.Contains(component.Position))
                            _groupSelection.Components.Add(id);
                    }

                    foreach (var wireId in sim.WireIds)
                    {
                        ref Wire wire = ref sim.Wire(wireId);
                        if (bounds.Contains(wire.A))
                            _groupSelection.WireNodes.Add((wireId, true));
                        if (bounds.Contains(wire.B))
                            _groupSelection.WireNodes.Add((wireId, false));
                    }
                    return;
                }
                return;
            }

            if (_groupSelection is not null && (Keys.LeftControl.Down() || Keys.RightControl.Down()) && Keys.C.Down())
            {
                _selectionCopied = new SelectionCopyData();
                Point min = new(int.MaxValue, int.MaxValue);
                Point max = new(int.MinValue, int.MinValue);

                foreach (var id in _groupSelection.Components)
                {
                    ref Component component = ref sim.GetComponent(id);
                    min = new Point(int.Min(min.X, component.Position.X), int.Min(min.Y, component.Position.Y));
                    max = new Point(int.Max(max.X, component.Position.X), int.Max(max.Y, component.Position.Y));

                    _selectionCopied.Components.Add((component.Blueprint, component.Position, component.Blueprint.Rotation));
                }

                foreach (var (wireId, _) in _groupSelection.WireNodes)
                {
                    ref Wire wire = ref sim.Wire(wireId);

                    min = new Point(int.Min(min.X, int.Min(wire.A.X, wire.B.X)), int.Min(min.Y, int.Min(wire.A.Y, wire.B.Y)));
                    max = new Point(int.Max(max.X, int.Max(wire.A.X, wire.B.X)), int.Max(max.Y, int.Max(wire.A.Y, wire.B.Y)));

                    _selectionCopied.Wires.Add(wire);
                }
                _selectionCopied.Wires = _selectionCopied.Wires.Distinct().ToList();

                _selectionCopied.Center = (min + max) / new Point(2);
            }

            if (_groupSelection is not null && (Keys.Back.Down() || Keys.Delete.Down()))
            {
                foreach (var id in _groupSelection.Components)
                {
                    sim.DestroyComponent(sim.GetComponent(id).Position);
                }

                foreach (var (wireId, _) in _groupSelection.WireNodes)
                {
                    sim.DestroyWire(wireId);
                }

                _groupSelection = null;
                _selectionDragPrev = null;
                _selectRectangle = null;
            }

            if ((Keys.LeftControl.Down() || Keys.RightControl.Down()) && Keys.V.RisingEdge() && _selectionCopied is not null)
            {
                List<int> compIds = [];
                List<int> wireIds = [];

                foreach ((Blueprint blueprint, Point position, int rotation) in _selectionCopied.Components)
                {
                    var pos = position + tileOver - _selectionCopied.Center;
                    compIds.Add(sim.Place(blueprint, pos, rotation));
                }

                foreach(var wire in _selectionCopied.Wires)
                {
                    Wire w = wire;
                    w.A += tileOver - _selectionCopied.Center;
                    w.B += tileOver - _selectionCopied.Center;
                    wireIds.Add(sim.CreateWire(w));
                }

                _groupSelection = new GroupSelection()
                {
                    Components = compIds.Where(w => w != -1).ToList(),
                    WireNodes = wireIds.Where(w => w != -1).SelectMany(id =>
                    {
                        ref Wire wire = ref sim.Wire(id);
                        return ((int, bool)[])[(id, true), (id, false)];
                    }).ToList(),
                };
            }

            if(MouseButton.Left.RisingEdge())
            {
                _groupSelection = null;
                _selectionDragPrev = null;
                _selectRectangle = null;
            }
        }
    }

    private GlobalStateTable _globalStateTable;
    public GlobalStateTable State => _globalStateTable;

    public void Step()
    {
        _groupSelection = null;
        _globalStateTable.Reset();
        ErrDescription = ActiveEntry?.Blueprint.SimulateTick(_globalStateTable);
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

        if (MouseButton.Left.Down() && _draggedComponentId is null && _wireDragStart is null && _activeDragDrop is null && _selectRectangle is null && _selectionDragPrev is null)
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

        if(_selectRectangle is Rectangle bound)
        {
            bound = NormalizeRect(bound);

            _graphics.ShapeBatch.DrawRectangle(bound.Location.ToVector2(), bound.Size.ToVector2(), Color.White * 0.1f, Color.LightGray * 0.8f, 2);
        }

        if(_groupSelection is GroupSelection selection)
        {
            foreach(var componentId in selection.Components)
            {
                Point pos = ActiveSim.GetComponent(componentId).Position;

                _graphics.ShapeBatch.DrawCircle(pos.ToVector2() * Constants.Scale, 40, Color.Transparent, Color.White * 0.5f, 2);
            }

            foreach(var (wireId, side) in selection.WireNodes)
            {
                ref Wire w = ref ActiveSim.Wire(wireId);
                Point pos = side ? w.A : w.B;

                _graphics.ShapeBatch.DrawCircle(pos.ToVector2() * Constants.Scale, 20, Color.Transparent, Color.LightBlue * 0.5f, 2);
            }
        }
    }
    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-Constants.Scale / 2)) / new Vector2(Constants.Scale)).ToPoint();
    static Rectangle NormalizeRect(Rectangle r)
    {
        int x = r.Width < 0 ? r.X + r.Width : r.X;
        int y = r.Height < 0 ? r.Y + r.Height : r.Y;
        int w = Math.Abs(r.Width);
        int h = Math.Abs(r.Height);

        return new Rectangle(x, y, w, h);
    }
    private class GroupSelection
    {
        public List<int> Components { get; set; } = [];
        public List<(int Id, bool IsA)> WireNodes { get; set; } = [];
    }

    private class SelectionCopyData
    {
        public List<(Blueprint Blueprint, Point Position, int Rotation)> Components { get; set; } = [];
        public List<Wire> Wires { get; set; } = [];
        public Point Center { get; set; }
    }
}
