using System;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;
using Wires.Core;
using Apos.Shapes;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;

namespace Wires.Sim;

public class Simulation
{
    private readonly Tile[] _tiles;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public ref Tile this[int x, int y] => ref _tiles[x + y * Width];
    public ref Tile this[Point p] => ref this[p.X, p.Y];

    private FastStack<WorkItem> _workList = new FastStack<WorkItem>(4);
    private FastStack<int> _seedComponents = new FastStack<int>(4);

    // coord -> wire
    private ShortSparseSet<FastStack<WireNode>> _wireMap = new();
    private FreeStack<Wire> _wires = new FreeStack<Wire>(16);

    private FreeStack<Component> _components = new(16);

    private int _nextVisitId = 1;

    public Simulation(int width = 100, int height = 100)
    {
        _ = checked((ushort)width * (ushort)height);

        _tiles = new Tile[width * height];
        Width = width;
        Height = height;
    }

    public IEnumerable Step()
    {
        foreach (ref var wire in _wires.AsSpan())
        {
            wire.PowerState = default;
            wire.LastVisitComponentId = -1;
        }

        yield return null;

        int visitId = _nextVisitId++;


        for (int i = 0; i < _seedComponents.Count; i++)
        {
            int seedComponentId = _seedComponents[i];
            Component component = _components[seedComponentId];
            switch (component.Blueprint.Descriptor)
            {
                case Blueprint.IntrinsicBlueprint.On:
                    foreach (var wireNode in WiresAt(component.GetOutputPosition(0)))
                    {
                        VisitWire(wireNode, seedComponentId, new PowerState(1, true));
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.Off:
                    foreach (var wireNode in WiresAt(component.GetOutputPosition(0)))
                    {
                        VisitWire(wireNode, seedComponentId, new PowerState(0, true));
                    }
                    break;
                default:
                    throw new Exception("Not a seed component");
            }
            yield return null;
        }
        // do work

        while (_workList.TryPop(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            if (tile.Kind == TileKind.Input)
            {
                ref Component component = ref _components[tile.ComponentId];
                if (component.LastVisitId == visitId)
                    continue;

                switch (component.Blueprint.Descriptor)
                {
                    case Blueprint.IntrinsicBlueprint.Output:
                        // todo, read outputs
                        break;
                    case Blueprint.IntrinsicBlueprint.Transisstor:
                        if (PowerStateAt(component.GetInputPosition(1 /*base*/)) is { On: true })
                        {
                            // we got power - mark as visited
                            component.LastVisitId = visitId;

                            PowerState collectorPowerState = PowerStateAt(component.GetInputPosition(0/*collector*/));

                            // copy input state to output wires
                            foreach (WireNode connection in WiresAt(component.GetOutputPosition(0 /*emitter*/)))
                            {
                                Wire wire = _wires[connection.Id];

                                VisitWire(connection, tile.ComponentId, collectorPowerState);
                            }
                        }
                        break;
                    case Blueprint.IntrinsicBlueprint.Not:
                        PowerState inputState = PowerStateAt(component.GetInputPosition(0 /*base*/));
                        if (inputState is { IsInactive: false })
                        {
                            // we got power - mark as visited
                            component.LastVisitId = visitId;

                            // copy input state to output wires
                            foreach (WireNode connection in WiresAt(component.GetOutputPosition(0 /*emitter*/)))
                            {
                                Wire wire = _wires[connection.Id];

                                VisitWire(connection, tile.ComponentId, inputState.On ? new PowerState(0, true) : new PowerState(1, true));
                            }
                        }
                        break;
                    default: throw new NotImplementedException();
                }
            }

            yield return null;
        }

        void VisitWire(WireNode wireNode, int componentId, PowerState state)
        {
            if(state.IsInactive)
            {
                return;
            }

            ref Wire wire = ref _wires[wireNode.Id];
            wire.LastVisitComponentId = componentId;
            wire.PowerState = state;

            //_workList.PushRef() = new WorkItem(wireNode.To, state, componentId);

            // handle connecting wires
            // this is simlar to recursive flood fill
            foreach (WireNode connection in WiresAt(wireNode.To))
            {
                Wire connectedWire = _wires[connection.Id];

                if (connectedWire.LastVisitComponentId != -1)
                {// this wire has been powered already
                    if(connectedWire.LastVisitComponentId == componentId)
                        continue;
                    else if(connectedWire.PowerState != state)
                        throw new Exception("Short Circuit!");
                }

                connectedWire.PowerState = state;

                // copy power state to other wires
                VisitWire(connection, componentId, state);
            }
        }
    }



    readonly record struct WorkItem(Point Position, PowerState State, int ComponentId);

    public Span<WireNode> WiresAt(Point position)
    {
        ushort index = (ushort)(position.X + position.Y * Width);
        if(_wireMap.Has(index))
        {
            return _wireMap[index].AsSpan();
        }
        return Span<WireNode>.Empty;
    }

    public int? IdOfWireAt(Point position)
    {
        ushort index = (ushort)(position.X + position.Y * Width);
        if (_wireMap.Has(index))
        {
            foreach(var i in _wireMap[index].AsSpan())
            {
                return i.Id;
            }
        }
        return default;
    }

    private PowerState PowerStateAt(Point position)
    {
        foreach(var wireId in WiresAt(position))
        {
            ref var wire = ref _wires[wireId.Id];
            return wire.PowerState;
        }

        return default;
    }

    public int Place(Blueprint blueprint, Point position)
    {
        foreach(var item in blueprint.Display)
        {
            var pos = item.Offset + position;
            if (!InRange(pos) || this[pos.X, pos.Y].Kind != TileKind.Nothing)
            {
                return -1;
            }
        }

        _components.Create(out int id) = new() { Blueprint = blueprint, Position = position };

        if(blueprint.Descriptor == Blueprint.IntrinsicBlueprint.On ||
            blueprint.Descriptor == Blueprint.IntrinsicBlueprint.Off)
        {
            _seedComponents.PushRef() = id;
        }

        foreach (var item in blueprint.Display)
        {
            var pos = item.Offset + position;
            this[pos.X, pos.Y] = new Tile()
            {
                Kind = item.Kind,
                ComponentId = id
            };
        }

        return id;
    }

    public void DestroyWire(int wireId)
    {
        Wire w = _wires[wireId];
        _wires.Destroy(wireId);

        ushort aIndex = checked((ushort)(w.A.X + w.A.Y * Width));
        _wireMap[aIndex].LazyInit().Remove(new WireNode() { Id = wireId, To = w.B });

        ushort bIndex = checked((ushort)(w.B.X + w.B.Y * Width));
        _wireMap[bIndex].LazyInit().Remove(new WireNode() { Id = wireId, To = w.A });
    }

    public bool InRange(Point pos)
    {
        return (uint)pos.X < (uint)Width && (uint)pos.Y < (uint)Height;
    }

    public int CreateWire(Wire w)
    {
        _wires.Create(out int id) = w;

        ushort aIndex = checked((ushort)(w.A.X + w.A.Y * Width));
        _wireMap[aIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.B };

        ushort bIndex = checked((ushort)(w.B.X + w.B.Y * Width));
        _wireMap[bIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.A };

        return id;
    }

    public void Draw(ShapeBatch sb, Vector2 scale, float wireRad)
    {
        Vector2 halfTileOffset = scale * 0.5f;

        foreach (ref var component in _components.AsSpan())
        {
            Point pos = component.Position;
            foreach ((Point offset, TileKind kind) in component.Blueprint.Display.AsSpan())
            {
                Point tilePos = pos + offset;
                Vector2 mapPos = tilePos.ToVector2() * scale;

                switch (kind)
                {
                    case TileKind.Input: sb.FillEquilateralTriangle(mapPos, wireRad * 0.7f, new Color(14, 92, 181), 0, MathHelper.PiOver2); break;
                    case TileKind.Output: sb.FillRectangle(mapPos - new Vector2(wireRad), new(wireRad * 2), Color.Orange); break;
                    case TileKind.Component: sb.FillRectangle(mapPos - halfTileOffset, scale, new Color(181, 14, 59), 8); break;
                }
            }
        }

        foreach (ref var wire in _wires.AsSpan())
        {
            if (!wire.Exists)
                continue;
            Color color;
            Color outline;
            if (wire.PowerState.IsInactive)
            {
                outline = Color.Gray;
                color = Color.DarkGray;
            }
            else
            {
                (color, outline) = wire.PowerState.On ?
                    (Color.Green, Color.DarkGreen) :
                    (Color.Red, Color.DarkRed);
            }
            sb.DrawLine(wire.A.ToVector2() * scale, wire.B.ToVector2() * scale, wireRad, color, outline, 4);
        }
    }
}