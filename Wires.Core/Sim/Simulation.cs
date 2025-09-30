using System;
using Microsoft.Xna.Framework;
using Wires.Core;
using Apos.Shapes;
using System.Collections.Generic;
using System.Diagnostics;
using Paper.Core;

namespace Wires.Core.Sim;

public class Simulation
{
    public ShortCircuitDescription? CurrentShortCircuit { get; private set; }

    private readonly Tile[] _tiles;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public ref Tile this[int x, int y] => ref _tiles[x + y * Width];
    public ref Tile this[Point p] => ref this[p.X, p.Y];

    private Queue<WorkItem> _workList = new Queue<WorkItem>(4);
    private HashSet<int> _seedComponents = [];
    private HashSet<int> _delayComponentIds = new(4);

    public int DelayCount => _delayComponentIds.Count;

    // coord -> wire
    private ShortSparseSet<FastStack<WireNode>> _wireMap = new();
    private FreeStack<Wire> _wires = new FreeStack<Wire>(16);

    private FreeStack<Component> _components = new(16);

    private Dictionary<Point, PowerState> _outputs = [];

    public Simulation(int width = 24, int height = 24)
    {
        _ = checked((ushort)width * (ushort)height);

        _tiles = new Tile[width * height];
        Width = width;
        Height = height;
    }

    public ref Component GetComponent(int id) => ref _components[id];

    public IEnumerable<Component> Components
    {
        get
        {
            for (int i = 0; i < _components.Max; i++)
            {
                var c = _components[i];
                if (c.Exists)
                {
                    yield return c;
                }
            }
        }
    }

    public IEnumerable<Wire> Wires
    {
        get
        {
            for (int i = 0; i < _wires.Max; i++)
            {
                var c = _wires[i];
                if (c.Exists)
                {
                    yield return c;
                }
            }
        }
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

    public IEnumerable<ShortCircuitDescription?> StepEnumerator(Blueprint blueprint)
    {
        CurrentShortCircuit = null;
        _outputs.Clear();

        foreach (ref var wire in _wires.AsSpan())
        {
            wire.PowerState = PowerState.OffState;
            wire.LastVisitComponentId = -1;
        }

        yield return null;

        // also add components
        int id = 0;
        foreach (var component in _components.AsSpan())
        {
            if (!component.Exists)
                goto @continue;

            if (component.Blueprint is { Custom: not null, Inputs.Length: 0, Descriptor: not Blueprint.IntrinsicBlueprint.Input })
            {// cant bump components that have no inputs by pushing to work list
                component.Blueprint.StepStateful(false);

                for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                {
                    PowerState power = component.Blueprint.OutputBuffer(i);
                    Point outputPosition = component.GetOutputPosition(i);

                    if(StartVisit(component.GetOutputPosition(i), id, power) is { } err)
                    {
                        yield return CurrentShortCircuit = new ShortCircuitDescription(id, -1, default, default, -1, err);
                        yield break;
                    }
                }
                goto @continue;
            }

            foreach (var offset in component.Blueprint.Inputs)
            {
                if (ConnectedToAnOutput(component.Position))
                    goto @continue;

                _workList.Enqueue(new WorkItem(component.Position + offset, PowerState.OffState, id));
                // only need to update 1 input
                break;
            }

        @continue:
            id++;
        }

        foreach (var seedComponentId in _seedComponents)
        {
            Component component = _components[seedComponentId];
            Point firstOutputPos = component.GetOutputPosition(0);
            PowerState powerToApply = component.Blueprint.Descriptor switch
            {
                Blueprint.IntrinsicBlueprint.On => PowerState.OnState,
                Blueprint.IntrinsicBlueprint.Off => PowerState.OffState,
                Blueprint.IntrinsicBlueprint.Input => blueprint.InputBuffer(component.InputOutputId),
                Blueprint.IntrinsicBlueprint.Delay => component.Blueprint.DelayValue,
                Blueprint.IntrinsicBlueprint.Switch => component.Blueprint.SwitchValue,
                _ => throw new Exception("Not a seed component")
            };

            _outputs[firstOutputPos] = powerToApply;

            foreach (var wireNode in WiresAt(firstOutputPos))
            {
                VisitWire(wireNode, seedComponentId, powerToApply);
            }

            yield return null;
        }

        // do work

        while (_workList.TryDequeue(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            ref Component component = ref _components[tile.ComponentId];

            ShortCircuitDescription? shortCircuit = null;
            switch (component.Blueprint.Descriptor)
            {
                case Blueprint.IntrinsicBlueprint.Output:
                    // we dont always read outputs
                    blueprint.OutputBuffer(component.InputOutputId) = w.State;
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
                case Blueprint.IntrinsicBlueprint.Delay:
                    // read delay value

                    PowerState outputDelayValue = component.Blueprint.DelayValue;

                    shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, outputDelayValue);
                    break;
                case Blueprint.IntrinsicBlueprint.Splitter:
                    PowerState powerState = w.State;

                    for (int i = 0; i < 8; i++)
                    {
                        StartVisit(component.GetOutputPosition(i), tile.ComponentId, ((1 << i) & powerState.Values) != 0 ? PowerState.OnState : PowerState.OffState);
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.Joiner:
                    PowerState outputState = PowerState.OffState;

                    for (int i = 0; i < 8; i++)
                    {
                        if(PowerStateAt(component.GetInputPosition(i)).On)
                        {
                            outputState.Values |= (byte)(1 << i);
                        }
                    }

                    StartVisit(component.GetOutputPosition(0), tile.ComponentId, outputState);
                    break;
                case Blueprint.IntrinsicBlueprint.AND:
                case Blueprint.IntrinsicBlueprint.NAND:
                case Blueprint.IntrinsicBlueprint.OR:
                case Blueprint.IntrinsicBlueprint.NOR:
                case Blueprint.IntrinsicBlueprint.XOR:
                case Blueprint.IntrinsicBlueprint.XNOR:
                    PowerState a = PowerStateAt(component.GetInputPosition(0));
                    PowerState b = PowerStateAt(component.GetInputPosition(1));

                    PowerState outputPowerState = component.Blueprint.Descriptor switch
                    {
                        Blueprint.IntrinsicBlueprint.AND =>     (a.On & b.On),
                        Blueprint.IntrinsicBlueprint.NAND =>   !(a.On & b.On),
                        Blueprint.IntrinsicBlueprint.OR =>      (a.On | b.On),
                        Blueprint.IntrinsicBlueprint.NOR =>    !(a.On | b.On),
                        Blueprint.IntrinsicBlueprint.XOR =>     (a.On ^ b.On),
                        Blueprint.IntrinsicBlueprint.XNOR =>   !(a.On ^ b.On),
                        _ => throw new UnreachableException()
                    } ? PowerState.OnState : PowerState.OffState;

                    shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, outputPowerState);
                    break;
                case Blueprint.IntrinsicBlueprint.NOT:
                    PowerState a1 = PowerStateAt(component.GetInputPosition(0));
                    shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, a1.On ? PowerState.OffState : PowerState.OnState);
                    break;
                case Blueprint.IntrinsicBlueprint.Disp:
                    // nothing
                    break;
                case Blueprint.IntrinsicBlueprint.None:
                    if (component.Blueprint.Custom is null)
                        goto default;
                    // custom
                    for(int i = 0; i < component.Blueprint.Inputs.Length; i++)
                        component.Blueprint.InputBuffer(i) = PowerStateAt(component.GetInputPosition(i));

                    component.Blueprint.OutputBufferRaw.AsSpan().Clear();

                    if(component.Blueprint.StepStateful(false) is ShortCircuitDescription pow)
                    {
                        yield return CurrentShortCircuit = new ShortCircuitDescription(tile.ComponentId, -1, default, default, -1, pow);
                        yield break;
                    }

                    for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                    {
                        PowerState power = component.Blueprint.OutputBuffer(i);
                        Point outputPosition = component.GetOutputPosition(i);

                        shortCircuit = StartVisit(component.GetOutputPosition(i), tile.ComponentId, power);
                    }
                    break;
                default: throw new NotImplementedException();
            }

            if(shortCircuit is ShortCircuitDescription err)
            {
                yield return err;
                yield break;
            }
            else
            {
                yield return null;
            }
        }

        // before returning, update inputs of delay components

        ShortCircuitDescription? StartVisit(Point point, int componentId, PowerState state)
        {
            _outputs[point] = state;

            foreach (WireNode connection in WiresAt(point))
            {
                if (VisitWire(connection, componentId, state) is ShortCircuitDescription shortCircuitDescription)
                    return shortCircuitDescription;
            }
            return null;
        }

        ShortCircuitDescription? VisitWire(WireNode wireNode, int componentId, PowerState state)
        {
            ref Wire wire = ref _wires[wireNode.Id];

            if (wire.PowerState == state)
                return null;
            
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
                    if (connectedWire.LastVisitComponentId == componentId)
                    {
                        if (connectedWire.PowerState == state)
                            continue;
                    }
                    else if (connectedWire.PowerState != state)
                        return CurrentShortCircuit = new ShortCircuitDescription(connectedWire.LastVisitComponentId, componentId, connectedWire.PowerState, state, wireNode.Id);
                }

                connectedWire.PowerState = state;

                // copy power state to other wires
                if(VisitWire(connection, componentId, state) is ShortCircuitDescription err)
                {
                    return err;
                }
            }

            return null;
        }

        bool ConnectedToAnOutput(Point connection)
        {
            foreach(var wireNode in WiresAt(connection))
            {
                Wire wire = _wires[wireNode.Id];
                if (wire.LastVisitComponentId == -2)
                    continue;// mark visited with -2
                wire.LastVisitComponentId = -2;

                if (this[wireNode.To].Kind is TileKind.Output)
                    return true;

                if (ConnectedToAnOutput(wireNode.To))
                    return true;
            }

            return false;
        }
    }

    public void RecordDelayValues()
    {
        foreach (var delayComponentId in _delayComponentIds)
        {
            Component delayComponent = _components[delayComponentId];
            delayComponent.Blueprint.DelayValue = PowerStateAt(delayComponent.GetInputPosition(0));
        }

        foreach(var component in _components.AsSpan())
        {
            if (!component.Exists)
                continue;

            if(component.Blueprint.Custom is { } b)
            {
                b.RecordDelayValues();
            }
        }
    }

    public void ClearAllDelayValues()
    {
        foreach(var component in _components.AsSpan())
        {
            if (!component.Exists)
                continue;

            var descript = component.Blueprint.Descriptor;
            if (descript == Blueprint.IntrinsicBlueprint.Delay)
            {
                component.Blueprint.DelayValue = PowerState.OffState;
            }
            else if(descript == Blueprint.IntrinsicBlueprint.None)
            {
                component.Blueprint.Custom!.ClearAllDelayValues();
            }
        }
    }
    private FastStack<(Point A, Point B, int Id)> _wiresToMove = new(4);
    public bool MoveComponent(int componentId, Point to)
    {
        ref Component component = ref _components[componentId];

        if (component.Position == to)
            return true;

        Point delta = to - component.Position;

        foreach(var element in component.Blueprint.Display)
        {
            Point destPos = element.Offset + to;
            if(!InRange(destPos) || (this[destPos] is { Kind: not TileKind.Nothing } dest && dest.ComponentId != componentId))
            {
                return false;
            }
        }

        //foreach(var element in component.Blueprint.Display)
        //{
        //    if(element.Kind is TileKind.Output or TileKind.Input)
        //    {
        //        Point oldWirePos = component.Position + element.Offset;
        //        // all separate to avoid messing up the data structures
        //        foreach(var node in WiresAt(oldWirePos))
        //        {
        //            _wiresToMove.PushRef() = (node.To, oldWirePos + delta, node.Id);
        //        }
        //    }
        //}
        //
        //foreach (var wire in _wiresToMove.AsSpan())
        //{
        //    DestroyWire(wire.Id);
        //}
        //while (_wiresToMove.TryPop(out var x))
        //{
        //    CreateWire(new Wire(x.A, x.B));
        //}

        foreach (var element in component.Blueprint.Display)
        {
            Point fromPos = element.Offset + component.Position;
            this[fromPos] = default;
        }

        foreach (var element in component.Blueprint.Display)
        {
            Point destPos = element.Offset + to;
            this[destPos] = new() { ComponentId = componentId, Kind = element.Kind };
        }

        component.Position = to;

        return true;
    }

    readonly record struct WorkItem(Point Position, PowerState State, int ComponentId);

    public Span<WireNode> WiresAt(Point position)
    {
        ushort index = (ushort)(position.X + position.Y * Width);
        if(_wireMap.TryGet(index, out var stack))
        {
            return stack.AsSpan();
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

    public int Place(Blueprint blueprint, Point position, int rotation, bool allowDelete = true, int inputOutputId = 0, bool initalState = false)
    {
        blueprint = blueprint.Clone(rotation);

        foreach(var item in blueprint.Display)
        {
            var pos = item.Offset + position;
            if (!InRange(pos) || this[pos.X, pos.Y].Kind != TileKind.Nothing)
            {
                return -1;
            }
        }

        _components.Create(out int id) = new() { Blueprint = blueprint, Position = position, Exists = true, AllowDelete = allowDelete, InputOutputId = inputOutputId };

        if(blueprint.Descriptor is Blueprint.IntrinsicBlueprint.On or 
            Blueprint.IntrinsicBlueprint.Off or 
            Blueprint.IntrinsicBlueprint.Input or 
            Blueprint.IntrinsicBlueprint.Delay or 
            Blueprint.IntrinsicBlueprint.Switch)
        {
            _seedComponents.Add(id);

            if(blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Switch)
            {
                blueprint.SwitchValue = initalState ? PowerState.OnState : PowerState.OffState;
            }
        }

        if(blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Delay)
        {
            _delayComponentIds.Add(id);
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
            var pos = item.Offset + component.Position;
            ref var x = ref this[pos.X, pos.Y];
            x = new() { ComponentId = -1, Kind = TileKind.Nothing };
        }

        _seedComponents.Remove(componentId);
        _delayComponentIds.Remove(componentId);

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

        ref var wire = ref _wires.Create(out int id);
        wire.A = w.A;
        wire.B = w.B;

        ushort aIndex = checked((ushort)(w.A.X + w.A.Y * Width));
        _wireMap[aIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.B };

        ushort bIndex = checked((ushort)(w.B.X + w.B.Y * Width));
        _wireMap[bIndex].LazyInit().PushRef() = new WireNode() { Id = id, To = w.A };

        return id;
    }

    public void Draw(Graphics g)
    {
        ShapeBatch sb = g.ShapeBatch;

        const float Scale = Constants.Scale;

        if(InputHelper.Down(Microsoft.Xna.Framework.Input.Keys.P))
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    if (this[i, j].Kind == TileKind.Nothing)
                        continue;
                    g.DrawStringCentered(this[i, j].Kind.ToString(), new Vector2(i, j) * Scale, 0.7f);
                }
            }
        }

        DrawGrid(sb, new Vector2(Scale) * -0.5f, Scale);

        Vector2 halfTileOffset = new Vector2(Scale) * 0.5f;

        int shortCircuitComponentId = CurrentShortCircuit is { Next: not null } ? CurrentShortCircuit.ComponentIdA : -1;

        int id = 0;
        foreach (ref var component in _components.AsSpan())
        {
            if (!component.Exists)
                continue;

            component.Blueprint.Draw(g, this, component.Position, Scale, Constants.WireRad, id == shortCircuitComponentId, component.InputOutputId);
            id++;
        }

        foreach (ref var wire in _wires.AsSpan())
        {
            if (!wire.Exists)
                continue;
            var (color, outline) = Constants.GetWireColor(wire.PowerState);

            DrawWire(sb, Scale, wire, color, outline);
        }

        if(CurrentShortCircuit is { WireId: > 0 })
        {
            Wire w = _wires[CurrentShortCircuit.WireId];

            DrawWire(sb, Scale, w, Color.Yellow, Color.DarkGoldenrod);
        }
    }

    public void DrawWire(ShapeBatch sb, float scale, Wire wire, Color color, Color outline)
    {
        var b = wire.B.ToVector2() * scale;
        var a = wire.A.ToVector2() * scale;

        sb.DrawLine(a,
                    b, Constants.WireRad, color, outline, 4);
        
        Node(wire.A, a);
        Node(wire.B, b);

        void Node(Point point, Vector2 a)
        {
            sb.DrawCircle(a, Constants.WireRad * 1.45f, color, outline, 4);
            Color thatGrayColor = new Color(64, 64, 64);
            if (InRange(wire.A))
            {
                var k = this[point].Kind;
                if (k is TileKind.Output)
                {
                    sb.DrawEquilateralTriangle(a, Constants.WireRad * 0.35f, thatGrayColor, outline, 0);
                    return;
                }
                else if(k is TileKind.Input)
                {
                    Vector2 size = new(Constants.WireRad * 0.85f);
                    sb.DrawRectangle(a - size * 0.5f, size, thatGrayColor, outline, 0);
                    return;
                }
            }


            sb.DrawCircle(a, Constants.WireRad * 0.5f, thatGrayColor, outline, 0);
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