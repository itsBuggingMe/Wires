using Frent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
using Wires.Core.Components.Stateful;
using Wires.Core.Sim.Components;
using Wires.Core.UI;

namespace Wires.Core.Sim;

internal class SimInteraction
{
    private ComponentEntry? _activeDragDrop;
    private int _rotation;
    private Entity _draggedComponentId;
    private Point? _wireDragStart;
    private bool _currentPlacedIsBundle = true;
    private Point _wireDragCurrent;
    private GroupSelection? _groupSelection;

    private readonly Camera2D _camera;
    private readonly Graphics _graphics;

    private Rectangle? _selectRectangle;
    private Point? _selectionDragPrev;
    private SelectionCopyData? _selectionCopied;
    private readonly Stack<SimulationSnapshot> _undoStack = [];
    private readonly Stack<SimulationSnapshot> _redoStack = [];
    private bool _dragUndoCaptured;
    private SimulationComment? _activeComment;
    private int _commentFrames;

    public TickResult? TickResult { get; private set; }

    public ComponentEntry? ActiveEntry
    { 
        get;
        set
        {
            Reset();
            _undoStack.Clear();
            _redoStack.Clear();
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
            bool controlDown = Keys.LeftControl.Down() || Keys.RightControl.Down();

            if (controlDown && Keys.Z.RisingEdge())
            {
                Undo(sim);
                return;
            }

            if (controlDown && Keys.Y.RisingEdge())
            {
                Redo(sim);
                return;
            }

            if (controlDown && Keys.N.RisingEdge())
            {
                RecordUndo(sim);
                _activeComment = new SimulationComment(tileOver);
                sim.Comments.Add(_activeComment);
                SimulationChanged?.Invoke();
                return;
            }

            if (MouseButton.Left.RisingEdge() && CommentAt(sim, tileOver) is { } clickedComment)
            {
                _activeComment = clickedComment;
                return;
            }

            if (_activeComment is not null)
            {
                if (UpdateActiveComment(sim, controlDown))
                    return;

                if (MouseButton.Left.RisingEdge())
                {
                    _activeComment = null;
                    return;
                }
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
                    Rectangle bounds = ExpandSelectionBounds(NormalizeRect(_selectRectangle.Value));

                    _selectRectangle = null;
                    _groupSelection = new GroupSelection();

                    foreach (var id in sim.Components.EnumerateWithEntities())
                    {
                        ref ComponentData component = ref id.Get<ComponentData>();
                        if(ComponentIntersectsSelection(bounds, component))
                            _groupSelection.Components.Add(id);
                    }

                    foreach (var wireId in sim.Wires.EnumerateWithEntities())
                    {
                        ref Wire wire = ref wireId.Get<Wire>();
                        if (TileCenterInSelection(bounds, wire.A))
                            _groupSelection.WireNodes.Add((wireId, true));
                        if (TileCenterInSelection(bounds, wire.B))
                            _groupSelection.WireNodes.Add((wireId, false));
                    }
                    return;
                }
                return;
            }

            // switch

            if (InputHelper.FallingEdge(MouseButton.Left))
            {
                foreach (var component in sim.Components.EnumerateWithEntities())
                {
                    // TODO: refactor into system
                    if (component.Get<ComponentData>().Position == tileOver && component.Get<ComponentData>() is { Blueprint.Descriptor: Blueprint.IntrinsicBlueprint.Switch })
                    {
                        RecordUndo(sim);
                        component.Get<ComponentData>().Blueprint.SwitchValue = component.Get<ComponentData>().Blueprint.SwitchValue.On ? PowerState.OffState : PowerState.OnState;
                        Step();
                        SimulationChanged?.Invoke();
                        return;
                    }
                }
            }

            // placing components
            if (_activeDragDrop is not null && InputHelper.FallingEdge(MouseButton.Left))
            {
                if(_activeDragDrop.Blueprint.Custom != sim)
                {
                    RecordUndo(sim);
                    if (sim.Place(_activeDragDrop.Blueprint, GetTileOver(), _rotation).IsAlive)
                    {
                        Step();
                        SimulationChanged?.Invoke();
                    }
                    else
                    {
                        DropUndo();
                    }
                }
                return;
            }
            
            if (_activeDragDrop is not null && InputHelper.Down(MouseButton.Right))
            {
                RecordUndo(sim);
                if (sim.Place(_activeDragDrop.Blueprint, GetTileOver(), _rotation).IsAlive)
                {
                    Step();
                    SimulationChanged?.Invoke();
                }
                else
                {
                    DropUndo();
                }
                return;
            }

            if (_activeDragDrop is not null && InputHelper.FallingEdge(Keys.Space))
            {
                _rotation++;
                return;
            }

            if (_groupSelection is { } rotateSelection && Keys.R.RisingEdge())
            {
                RecordUndo(sim);
                if (sim.RotateMany(rotateSelection.Components, rotateSelection.WireNodes))
                {
                    Step(clearSelection: false);
                    SimulationChanged?.Invoke();
                }
                else
                {
                    DropUndo();
                }
                return;
            }

            if (_groupSelection is { } selection && MouseButton.Left.RisingEdge() && _selectionDragPrev is null && IsSelectionHandle(selection, tileOver))
            {
                _selectionDragPrev = tileOver;
                RecordUndo(sim);
                return;
            }

            if (_selectionDragPrev is Point previousTile && _groupSelection is { } activeSelection)
            {
                if (!MouseButton.Left.Down())
                {
                    _selectionDragPrev = null;
                    return;
                }

                Point delta = tileOver - previousTile;
                if (delta != default && sim.MoveMany(activeSelection.Components, activeSelection.WireNodes, delta))
                {
                    _selectionDragPrev = tileOver;
                    Step(clearSelection: false);
                    SimulationChanged?.Invoke();
                }
                return;
            }

            // placing wires
            if (InputHelper.RisingEdge(MouseButton.Left) && sim.InRange(tileOver) && sim[tileOver] is { Kind: TileKind.Nothing } && !sim.HasWiresAt(tileOver) && sim.WireAtPath(GetTileOverVec2()) is { IsAlive: true } wireToSplit)
            {
                RecordUndo(sim);
                if (sim.SplitWireAt(wireToSplit, tileOver))
                {
                    _wireDragStart = tileOver;
                    _wireDragCurrent = tileOver;
                    _currentPlacedIsBundle = false;
                    Step();
                    SimulationChanged?.Invoke();
                }
                else
                {
                    DropUndo();
                }
                return;
            }

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

                RecordUndo(sim);
                Entity created = sim.CreateWire(new Wire(_wireDragStart.Value, _wireDragCurrent, _currentPlacedIsBundle ? WireKind.Byte : WireKind.Bit));
                _wireDragStart = null;
                if (created.IsAlive)
                {
                    Step();
                    SimulationChanged?.Invoke();
                }
                else
                {
                    DropUndo();
                }
                return;
            }

            // moving components
            if (MouseButton.Left.RisingEdge() && sim.InRange(tileOver) &&
                (sim[tileOver].Kind is TileKind.Output or TileKind.Input or TileKind.Component))
            {
                _draggedComponentId = sim[tileOver].Meta;
                _dragUndoCaptured = false;
                return;
            }

            if (_draggedComponentId.IsAlive && sim.InRange(tileOver) && !sim.HasWiresAt(tileOver))
            {
                if (!_dragUndoCaptured)
                {
                    RecordUndo(sim);
                    _dragUndoCaptured = true;
                }

                if (sim.MoveComponent(_draggedComponentId, tileOver))
                {
                    Step();
                    SimulationChanged?.Invoke();
                }
                if (!InputHelper.Down(MouseButton.Left))
                {
                    _draggedComponentId = Entity.Null;
                    _dragUndoCaptured = false;
                }
                return;
            }

            if (MouseButton.Right.FallingEdge() && _activeDragDrop is null && sim.InRange(tileOver))
            {
                if (sim.WireNodeAt(tileOver) is { IsAlive: true } wire)
                {
                    RecordUndo(sim);
                    if (sim.MergeWiresAt(tileOver))
                    {
                        SimulationChanged?.Invoke();
                        Step();
                    }
                    else
                    {
                        wire.Get<WireNode>().Wire.Delete();
                        SimulationChanged?.Invoke();
                        Step();
                    }
                }
                else if (sim.WireAtPath(GetTileOverVec2()) is { IsAlive: true } wireOnPath)
                {
                    RecordUndo(sim);
                    wireOnPath.Delete();
                    SimulationChanged?.Invoke();
                    Step();
                }
                else if (sim[tileOver].Kind is not TileKind.Nothing)
                {
                    RecordUndo(sim);
                    if (TryDeleteComponent(sim[tileOver].Meta))
                    {
                        SimulationChanged?.Invoke();
                        Step();
                    }
                    else
                    {
                        DropUndo();
                    }
                }
                return;
            }

            if (_groupSelection is not null && controlDown && Keys.C.RisingEdge())
            {
                CopySelection();
                return;
            }

            if (_groupSelection is not null && (Keys.Back.RisingEdge() || Keys.Delete.RisingEdge()))
            {
                RecordUndo(sim);
                foreach (var id in _groupSelection.Components)
                {
                    TryDeleteComponent(id);
                }

                foreach (var (wireId, _) in _groupSelection.WireNodes)
                {
                    if (wireId.IsAlive)
                        wireId.Delete();
                }

                _groupSelection = null;
                _selectionDragPrev = null;
                _selectRectangle = null;
                Step(clearSelection: false);
                SimulationChanged?.Invoke();
                return;
            }

            if (controlDown && Keys.V.RisingEdge() && _selectionCopied is not null)
            {
                List<Entity> compIds = [];
                List<Entity> wireIds = [];

                RecordUndo(sim);
                foreach ((Blueprint blueprint, Point position, int rotation, bool switchState) in _selectionCopied.Components)
                {
                    var pos = position + tileOver - _selectionCopied.Center;
                    compIds.Add(sim.Place(blueprint, pos, rotation, initalState: switchState));
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
                    Components = compIds.Where(w => w.IsAlive).ToList(),
                    WireNodes = wireIds.Where(w => w.IsAlive).SelectMany(id =>
                    {
                        ref Wire wire = ref id.Get<Wire>();
                        return ((Entity, bool)[])[(id, true), (id, false)];
                    }).ToList(),
                };

                Step();
                SimulationChanged?.Invoke();
                return;
            }

            if(MouseButton.Left.RisingEdge())
            {
                _groupSelection = null;
                _selectionDragPrev = null;
                _selectRectangle = null;
            }
        }
    }

    private void CopySelection()
    {
        if (_groupSelection is null)
            return;

        _selectionCopied = new SelectionCopyData();
        Point min = new(int.MaxValue, int.MaxValue);
        Point max = new(int.MinValue, int.MinValue);

        foreach (var id in _groupSelection.Components.Where(id => id.IsAlive))
        {
            ref ComponentData component = ref id.Get<ComponentData>();
            min = new Point(int.Min(min.X, component.Position.X), int.Min(min.Y, component.Position.Y));
            max = new Point(int.Max(max.X, component.Position.X), int.Max(max.Y, component.Position.Y));

            _selectionCopied.Components.Add((
                component.Blueprint,
                component.Position,
                component.Blueprint.Rotation,
                component.Blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Switch && component.Blueprint.SwitchValue.On));
        }

        foreach (var wireId in _groupSelection.WireNodes.Select(w => w.Id).Where(id => id.IsAlive).Distinct())
        {
            ref Wire wire = ref wireId.Get<Wire>();

            min = new Point(int.Min(min.X, int.Min(wire.A.X, wire.B.X)), int.Min(min.Y, int.Min(wire.A.Y, wire.B.Y)));
            max = new Point(int.Max(max.X, int.Max(wire.A.X, wire.B.X)), int.Max(max.Y, int.Max(wire.A.Y, wire.B.Y)));

            _selectionCopied.Wires.Add(wire);
        }

        if (_selectionCopied.Components.Count == 0 && _selectionCopied.Wires.Count == 0)
        {
            _selectionCopied = null;
            return;
        }

        _selectionCopied.Center = (min + max) / new Point(2);
    }

    private bool IsSelectionHandle(GroupSelection selection, Point tile)
    {
        foreach (var componentId in selection.Components.Where(id => id.IsAlive))
        {
            ref ComponentData component = ref componentId.Get<ComponentData>();
            foreach ((Point offset, _) in component.Blueprint.Display)
            {
                if (component.Position + offset == tile)
                    return true;
            }
        }

        foreach (var (wireId, side) in selection.WireNodes)
        {
            if (!wireId.IsAlive)
                continue;

            ref Wire wire = ref wireId.Get<Wire>();
            if ((side ? wire.A : wire.B) == tile)
                return true;
        }

        return false;
    }

    private void RecordUndo(Simulation simulation)
    {
        _undoStack.Push(simulation.CreateSnapshot());
        _redoStack.Clear();
    }

    private void DropUndo()
    {
        if (_undoStack.Count > 0)
            _undoStack.Pop();
    }

    private void Undo(Simulation simulation)
    {
        if (_undoStack.Count == 0)
            return;

        _redoStack.Push(simulation.CreateSnapshot());
        simulation.RestoreSnapshot(_undoStack.Pop());
        _groupSelection = null;
        _selectionDragPrev = null;
        _activeComment = null;
        Step();
        SimulationChanged?.Invoke();
    }

    private void Redo(Simulation simulation)
    {
        if (_redoStack.Count == 0)
            return;

        _undoStack.Push(simulation.CreateSnapshot());
        simulation.RestoreSnapshot(_redoStack.Pop());
        _groupSelection = null;
        _selectionDragPrev = null;
        _activeComment = null;
        Step();
        SimulationChanged?.Invoke();
    }

    private static bool TryDeleteComponent(Entity component)
    {
        if (!component.IsAlive)
            return false;

        if (!component.Get<ComponentData>().Deletable)
            return false;

        component.Delete();
        return true;
    }

    private GlobalStateTable _globalStateTable;
    public GlobalStateTable State => _globalStateTable;

    public Action? SimulationChanged { get; set; }

    public void Step(bool clearSelection = true)
    {
        if (clearSelection)
            _groupSelection = null;
        _globalStateTable.Reset();
        TickResult = ActiveEntry?.Blueprint.SimulateTick(_globalStateTable);
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

        if (MouseButton.Left.Down() && _draggedComponentId.IsNull && _wireDragStart is null && _activeDragDrop is null && _selectRectangle is null && _selectionDragPrev is null)
            _camera.Position -= (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2()) / _camera.Scale;
    }

    public void Reset()
    {
        _wireDragStart = default;
        _draggedComponentId = default;
        _activeDragDrop = default;
        _rotation = default;
        _activeComment = null;
    }

    public void BeginPlaceComponent(ComponentEntry componentEntry)
    {
        _activeDragDrop = componentEntry;
    }

    public void Draw()
    {
        if (ActiveSim is null)
            return;

        DrawComments(ActiveSim);

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
            ActiveSim.DrawWire(_graphics, Constants.Scale, new Wire(dragStart, _wireDragCurrent, default), colors.Color * 0.5f, colors.Output * 0.5f);
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
                Point pos = componentId.Get<ComponentData>().Position;

                _graphics.ShapeBatch.DrawCircle(pos.ToVector2() * Constants.Scale, 40, Color.Transparent, Color.White * 0.5f, 2);
            }

            foreach(var (wireId, side) in selection.WireNodes)
            {
                ref Wire w = ref wireId.Get<Wire>();
                Point pos = side ? w.A : w.B;

                _graphics.ShapeBatch.DrawCircle(pos.ToVector2() * Constants.Scale, 20, Color.Transparent, Color.LightBlue * 0.5f, 2);
            }
        }
    }
    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-Constants.Scale / 2)) / new Vector2(Constants.Scale)).ToPoint();
    private Vector2 GetTileOverVec2() => _camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) / Constants.Scale;

    private bool UpdateActiveComment(Simulation sim, bool controlDown)
    {
        if (_activeComment is null)
            return false;

        SimulationComment activeComment = _activeComment;

        if (Keys.Escape.RisingEdge() || Keys.Enter.RisingEdge())
        {
            _activeComment = null;
            return true;
        }

        if (Keys.Delete.RisingEdge())
        {
            RecordUndo(sim);
            sim.Comments.Remove(activeComment);
            _activeComment = null;
            SimulationChanged?.Invoke();
            return true;
        }

        if (Keys.Back.RisingEdge() && activeComment.Text.Length > 0)
        {
            RecordUndo(sim);
            activeComment.Text = activeComment.Text[..^1];
            SimulationChanged?.Invoke();
            return true;
        }

        if (controlDown)
            return false;

        foreach (var (key, character) in TextInput.CharMap)
        {
            if (!key.RisingEdge())
                continue;

            RecordUndo(sim);
            char toAppend = character;
            if (char.IsAsciiLetter(toAppend) && (Keys.LeftShift.Down() || Keys.RightShift.Down()))
                toAppend = char.ToUpperInvariant(toAppend);

            activeComment.Text += toAppend;
            SimulationChanged?.Invoke();
            return true;
        }

        return false;
    }

    private void DrawComments(Simulation sim)
    {
        _commentFrames++;
        Texture2D texture = _graphics.Content.Load<Texture2D>("comment");

        foreach (SimulationComment comment in sim.Comments)
        {
            Vector2 position = comment.Position.ToVector2() * Constants.Scale;
            Vector2 origin = texture.Bounds.Size.ToVector2() * 0.5f;
            float textureScale = Constants.Scale / MathF.Max(texture.Width, texture.Height);
            Color color = comment == _activeComment ? Color.White : Color.White * 0.85f;
            bool hovered = comment.Position == GetTileOver();

            _graphics.SpriteBatch.Draw(texture, position, null, color, 0, origin, textureScale, SpriteEffects.None, 0);

            if (!hovered)
                continue;

            string text = comment.Text;
            if (comment == _activeComment && (_commentFrames & 63) < 32)
                text += "|";

            if (text.Length > 0)
                _graphics.DrawString(text, position + new Vector2(Constants.Scale * 0.55f, -Constants.Scale * 0.25f), scale: 0.8f);
        }
    }

    private static SimulationComment? CommentAt(Simulation sim, Point tile)
    {
        for (int i = sim.Comments.Count - 1; i >= 0; i--)
        {
            if (sim.Comments[i].Position == tile)
                return sim.Comments[i];
        }

        return null;
    }

    static Rectangle NormalizeRect(Rectangle r)
    {
        int x = r.Width < 0 ? r.X + r.Width : r.X;
        int y = r.Height < 0 ? r.Y + r.Height : r.Y;
        int w = Math.Abs(r.Width);
        int h = Math.Abs(r.Height);

        return new Rectangle(x, y, w, h);
    }

    private static Rectangle ExpandSelectionBounds(Rectangle r)
    {
        const int ExpandAmount = Constants.Scale / 2;
        r.Inflate(ExpandAmount, ExpandAmount);
        return r;
    }

    private static bool ComponentIntersectsSelection(Rectangle selectionBounds, ComponentData component)
    {
        foreach ((Point offset, _) in component.Blueprint.Display)
        {
            if (TileCenterInSelection(selectionBounds, component.Position + offset))
                return true;
        }

        return false;
    }

    private static bool TileCenterInSelection(Rectangle selectionBounds, Point tile)
    {
        Vector2 center = tile.ToVector2() * Constants.Scale;
        return selectionBounds.Contains(center);
    }
    private class GroupSelection
    {
        public List<Entity> Components { get; set; } = [];
        public List<(Entity Id, bool IsA)> WireNodes { get; set; } = [];
    }

    private class SelectionCopyData
    {
        public List<(Blueprint Blueprint, Point Position, int Rotation, bool SwitchState)> Components { get; set; } = [];
        public List<Wire> Wires { get; set; } = [];
        public Point Center { get; set; }
    }
}
