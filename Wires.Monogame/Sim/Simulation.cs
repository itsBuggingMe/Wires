using System;
using Microsoft.Xna.Framework;
using Wires.Core;
using Apos.Shapes;
using System.Collections;
using System.Collections.Generic;

namespace Wires.Sim;

public class Simulation
{
    private readonly Tile[] _tiles;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public ref Tile this[int x, int y] => ref _tiles[x + y * Width];
    public ref Tile this[Point p] => ref this[p.X, p.Y];

    private Queue<WorkItem> _workList = new Queue<WorkItem>(4);
    private HashSet<int> _seedComponents = [];

    // coord -> wire
    private ShortSparseSet<FastStack<WireNode>> _wireMap = new();
    private FreeStack<Wire> _wires = new FreeStack<Wire>(16);

    private FreeStack<Component> _components = new(16);

    private int _nextVisitId = 1;

    private Dictionary<Point, PowerState> _outputs = [];

    public Simulation(int width = 24, int height = 24)
    {
        _ = checked((ushort)width * (ushort)height);

        _tiles = new Tile[width * height];
        Width = width;
        Height = height;
    }

    public int InputCount
    {
        get
        {
            int count = 0;
            foreach(var comp in _seedComponents)
                if (_components[comp].Blueprint.Descriptor == Blueprint.IntrinsicBlueprint.Input)
                    count++;
            return count;
        }
    }

    public int OutputCount
    {
        get
        {
            int count = 0;
            foreach (var comp in _components.AsSpan())
                if (comp is { Blueprint.Descriptor: Blueprint.IntrinsicBlueprint.Output, Exists: true })
                    count++;
            return count;
        }
    }

    public IEnumerable StepEnumerator(PowerState[] inputs, PowerState[] outputs)
    {
        _outputs.Clear();

        foreach (ref var wire in _wires.AsSpan())
        {
            wire.PowerState = PowerState.OffState;
            wire.LastVisitComponentId = -1;
        }

        yield return null;

        int visitId = _nextVisitId++;


        foreach(var seedComponentId in _seedComponents)
        {
            Component component = _components[seedComponentId];

            switch (component.Blueprint.Descriptor)
            {
                case Blueprint.IntrinsicBlueprint.On:
                    _outputs.TryAdd(component.GetOutputPosition(0), PowerState.OnState);
                    foreach (var wireNode in WiresAt(component.GetOutputPosition(0)))
                    {
                        VisitWire(wireNode, seedComponentId, PowerState.OnState);
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.Off:
                    _outputs.TryAdd(component.GetOutputPosition(0), PowerState.OffState);
                    foreach (var wireNode in WiresAt(component.GetOutputPosition(0)))
                    {
                        VisitWire(wireNode, seedComponentId, PowerState.OffState);
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.Input:
                    PowerState inputPower = inputs[component.InputOutputId];
                    _outputs.TryAdd(component.GetOutputPosition(0), inputPower);
                    foreach (var wireNode in WiresAt(component.GetOutputPosition(0)))
                    {
                        VisitWire(wireNode, seedComponentId, inputPower);
                    }
                    break;
                default:
                    throw new Exception("Not a seed component");
            }
            yield return null;
        }
        // do work

        while (_workList.TryDequeue(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            ref Component component = ref _components[tile.ComponentId];

            switch (component.Blueprint.Descriptor)
            {
                case Blueprint.IntrinsicBlueprint.Output:
                    // we dont always read outputs
                    if ((uint)component.InputOutputId < (uint)outputs.Length)
                        outputs[component.InputOutputId] = w.State;
                    break;
                case Blueprint.IntrinsicBlueprint.NAND:
                    component.LastVisitId = visitId;

                    PowerState a = PowerStateAt(component.GetInputPosition(0));
                    PowerState b = PowerStateAt(component.GetInputPosition(1));

                    PowerState outputPowerState = !(a.On && b.On) ? PowerState.OnState : PowerState.OffState; // nand!

                    _outputs.TryAdd(component.GetOutputPosition(0), outputPowerState);


                    foreach (WireNode connection in WiresAt(component.GetOutputPosition(0 /*emitter*/)))
                    {
                        VisitWire(connection, tile.ComponentId, outputPowerState);
                    }
                    break;
                //case Blueprint.IntrinsicBlueprint.Not:
                //    PowerState inputState = PowerStateAt(component.GetInputPosition(0));
                //    // we got power - mark as visited
                //    component.LastVisitId = visitId;
                //
                //    var state = inputState.On ? PowerState.OffState : PowerState.OnState;
                //    _outputs.TryAdd(component.GetOutputPosition(0), state);
                //
                //    // copy input state to output wires
                //    foreach (WireNode connection in WiresAt(component.GetOutputPosition(0 /*emitter*/)))
                //    {
                //        Wire wire = _wires[connection.Id];
                //
                //        VisitWire(connection, tile.ComponentId, state);
                //    }
                //    break;
                case Blueprint.IntrinsicBlueprint.None:
                    if (component.Blueprint.Custom is null)
                        goto default;
                    // custom
                    for(int i = 0; i < component.Blueprint.Inputs.Length; i++)
                        component.Blueprint.InputBuffer[i] = PowerStateAt(component.GetInputPosition(i));

                    component.Blueprint.StepStateful();

                    for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                    {
                        PowerState power = component.Blueprint.OutputBuffer[i];
                        Point outputPosition = component.GetOutputPosition(i);

                        foreach (WireNode connection in WiresAt(outputPosition))
                        {
                            VisitWire(connection, tile.ComponentId, power);
                        }
                    }
                    break;
                default: throw new NotImplementedException();
            }

            yield return null;
        }

        void VisitWire(WireNode wireNode, int componentId, PowerState state)
        {
            ref Wire wire = ref _wires[wireNode.Id];
            wire.LastVisitComponentId = componentId;
            wire.PowerState = state;

            if (this[wireNode.To].Kind is TileKind.Input)
                _workList.Enqueue(new WorkItem(wireNode.To, state, componentId));

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

    public bool HasWiresAt(Point position) => WiresAt(position).Length != 0;

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

    public PowerState PowerStateAt(Point position)
    {
        foreach(var wireId in WiresAt(position))
        {
            ref var wire = ref _wires[wireId.Id];
            return wire.PowerState;
        }

        if(_outputs.TryGetValue(position, out var v))
        {
            return v;
        }

        return default;
    }

    public int Place(Blueprint blueprint, Point position, bool allowDelete = true, int inputOutputId = 0)
    {
        foreach(var item in blueprint.Display)
        {
            var pos = item.Offset + position;
            if (!InRange(pos) || this[pos.X, pos.Y].Kind != TileKind.Nothing)
            {
                return -1;
            }
        }

        _components.Create(out int id) = new() { Blueprint = blueprint, Position = position, Exists = true, AllowDelete = allowDelete, InputOutputId = inputOutputId };

        if(blueprint.Descriptor == Blueprint.IntrinsicBlueprint.On ||
            blueprint.Descriptor == Blueprint.IntrinsicBlueprint.Off ||
            blueprint.Descriptor == Blueprint.IntrinsicBlueprint.Input)
        {
            _seedComponents.Add(id);
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

    public int DestroyComponent(Point position)
    {
        Tile t = this[position];
        if (t.Kind is not TileKind.Output and not TileKind.Input and not TileKind.Component)
            return -1;

        Component component = _components[t.ComponentId];

        if (!component.AllowDelete)
            return -1;

        int componentId = t.ComponentId;

        foreach (var item in component.Blueprint.Display)
        {
            var pos = item.Offset + position;
            this[pos.X, pos.Y] = new() { ComponentId = -1, Kind = TileKind.Nothing };
        }

        _seedComponents.Remove(componentId);

        _components.Destroy(componentId);

        return componentId;
    }

    public bool InRange(Point pos)
    {
        return (uint)pos.X < (uint)Width && (uint)pos.Y < (uint)Height;
    }

    public int CreateWire(Wire w)
    {
        if (w.A == w.B)
            return default;

        _wires.Create(out int id) = w;

        ushort aIndex = checked((ushort)(w.A.X + w.A.Y * Width));
        _wireMap[aIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.B };

        ushort bIndex = checked((ushort)(w.B.X + w.B.Y * Width));
        _wireMap[bIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.A };

        return id;
    }

    public void Draw(Graphics g, float scale)
    {
        ShapeBatch sb = g.ShapeBatch;

        if(InputHelper.Down(Microsoft.Xna.Framework.Input.Keys.P))
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    if (this[i, j].Kind == TileKind.Nothing)
                        continue;
                    g.DrawStringCentered(this[i, j].Kind.ToString(), new Vector2(i, j) * scale, 0.7f);
                }
            }
        }

        DrawGrid(sb, new Vector2(scale) * -0.5f, scale);

        Vector2 halfTileOffset = new Vector2(scale) * 0.5f;

        foreach (ref var component in _components.AsSpan())
        {
            if (!component.Exists)
                continue;

            component.Blueprint.Draw(g, this, component.Position, scale, Constants.WireRad);
        }

        foreach (ref var wire in _wires.AsSpan())
        {
            if (!wire.Exists)
                continue;
            var (color, outline) = Constants.GetWireColor(wire.PowerState);

            var a = wire.A.ToVector2() * scale;
            var b = wire.B.ToVector2() * scale;

            sb.DrawLine(a, 
                        b, Constants.WireRad, color, outline, 4);

            sb.DrawCircle(a, Constants.WireRad * 1.42f, color, outline, 4);
            sb.DrawCircle(a, Constants.WireRad * 0.5f, new Color(64, 64, 64), outline, 0);


            sb.DrawCircle(b, Constants.WireRad * 1.42f, color, outline, 4);
            sb.DrawCircle(b, Constants.WireRad * 0.5f, new Color(64, 64, 64), outline, 0);
        }
    }

    private void DrawGrid(ShapeBatch sb, Vector2 origin, float step)
    {
        const float LineWidth = 1f;

        float gridSizeX = Width * step;
        float gridSizeY = Height * step;

        for (int i = 0; i <= Width; i++)
        {
            float x = origin.X + i * step;
            sb.FillLine(
                new Vector2(x, origin.Y),
                new Vector2(x, origin.Y + gridSizeY),
                LineWidth,
                Constants.Accent
            );
        }

        for (int j = 0; j <= Height; j++)
        {
            float y = origin.Y + j * step;
            sb.FillLine(
                new Vector2(origin.X, y),
                new Vector2(origin.X + gridSizeX, y),
                LineWidth,
                Constants.Accent
            );
        }
    }
}