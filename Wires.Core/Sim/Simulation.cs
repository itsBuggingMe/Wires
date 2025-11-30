using Apos.Shapes;
using Frent.Core;
using Microsoft.Xna.Framework;
using Paper.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wires.Core;

namespace Wires.Core.Sim;

public class Simulation
{
    public ErrDescription? CurrentShortCircuit { get; private set; }

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

    public IEnumerable<int> ComponentIds
    {
        get
        {
            for (int i = 0; i < _components.Max; i++)
            {
                var c = _components[i];
                if (c.Exists)
                {
                    yield return i;
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

    public IEnumerable<int> WireIds
    {
        get
        {
            for (int i = 0; i < _wires.Max; i++)
            {
                var c = _wires[i];
                if (c.Exists)
                {
                    yield return i;
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

    private const int MaxIterationCount = 10_000;

    public IEnumerable<ErrDescription?> StepEnumerator(Blueprint blueprint, GlobalStateTable state, ulong previousAddressHash)
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
                component.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, component.Position));

                for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                {
                    PowerState power = component.Blueprint.OutputBuffer(i);
                    Point outputPosition = component.GetOutputPosition(i);

                    if(StartVisit(component.GetOutputPosition(i), id, power) is { } err)
                    {
                        yield return CurrentShortCircuit = new ErrDescription(id, -1, default, default, -1, err);
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
            // all seed comps only have 1 output
            PowerState powerToApply = component.Blueprint.Descriptor switch
            {
                Blueprint.IntrinsicBlueprint.On => PowerState.OnState,
                Blueprint.IntrinsicBlueprint.Off => PowerState.OffState,
                Blueprint.IntrinsicBlueprint.Input => blueprint.InputBuffer(component.InputOutputId),
                Blueprint.IntrinsicBlueprint.Delay => state[GlobalStateTable.CreateAddress(previousAddressHash, component.Position)],
                Blueprint.IntrinsicBlueprint.Switch => component.Blueprint.SwitchValue,
                _ => throw new Exception("Not a seed component")
            };

            _outputs[firstOutputPos] = powerToApply;

            yield return CurrentShortCircuit = StartVisit(firstOutputPos, seedComponentId, powerToApply);
        }

        // do work

        while (_workList.TryDequeue(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            ref Component component = ref _components[tile.ComponentId];

            ErrDescription? shortCircuit = null;
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
                    // once we hit a delay component, we stop processing further
                    // the responsibility of updating the input state of delay components is elsewhere
                    // reading the delay component is done as a seed component

                    //PowerState outputDelayValue = state[GlobalStateTable.CreateAddress(previousAddressHash, component.Position)];
                    //
                    //shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, outputDelayValue);
                    break;
                case Blueprint.IntrinsicBlueprint.FullAdder:
                    PowerState ia = PowerStateAt(component.GetInputPosition(0));
                    PowerState ib = PowerStateAt(component.GetInputPosition(1));
                    PowerState cin = PowerStateAt(component.GetInputPosition(2));

                    // sum
                    shortCircuit = 
                        StartVisit(component.GetOutputPosition(0), tile.ComponentId, 
                        (ia.On ^ ib.On ^ cin.On) ? PowerState.OnState : PowerState.OffState);
                    shortCircuit =
                        StartVisit(component.GetOutputPosition(1), tile.ComponentId,
                        ((ia.On && ib.On) || ((ia.On ^ ib.On) && cin.On)) ? PowerState.OnState : PowerState.OffState);
                    break;
                case Blueprint.IntrinsicBlueprint.Splitter:
                    PowerState powerState = w.State;

                    for (int i = 0; i < 8; i++)
                    {
                        if (StartVisit(component.GetOutputPosition(i), tile.ComponentId, ((1 << i) & powerState.Values) != 0 ? PowerState.OnState : PowerState.OffState) is { } d)
                        {
                            CurrentShortCircuit = shortCircuit = d;
                            break; 
                        }
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

                    shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, outputState);
                    break;
                case Blueprint.IntrinsicBlueprint.RAM:
                    var output = state.TickRam(
                        PowerStateAt(component.GetInputPosition(0)),
                        PowerStateAt(component.GetInputPosition(1)),
                        PowerStateAt(component.GetInputPosition(2)));
                    shortCircuit = StartVisit(component.GetOutputPosition(0), tile.ComponentId, output);
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
                case Blueprint.IntrinsicBlueprint.DEC8:
                    int index = 
                        (PowerStateAt(component.GetInputPosition(0)).On ? 0b001 : 0) |
                        (PowerStateAt(component.GetInputPosition(1)).On ? 0b010 : 0) |
                        (PowerStateAt(component.GetInputPosition(2)).On ? 0b100 : 0);

                    for(int i = 0; i < 8; i++)
                    {
                        shortCircuit = StartVisit(component.GetOutputPosition(i), tile.ComponentId, index == i ? PowerState.OnState : PowerState.OffState);
                    }
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

                    if(component.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, component.Position)) is ErrDescription pow)
                    {
                        yield return CurrentShortCircuit = new ErrDescription(tile.ComponentId, -1, default, default, -1, pow);
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

            if(shortCircuit is ErrDescription err)
            {
                yield return CurrentShortCircuit = err;
                yield break;
            }
            else
            {
                yield return null;
            }
        }

        RecordDelayValues(state, previousAddressHash);

        ErrDescription? StartVisit(Point point, int componentId, PowerState state)
        {
            _outputs[point] = state;

            foreach (WireNode connection in WiresAt(point))
            {
                if (VisitWire(connection, componentId, state) is ErrDescription ErrDescription)
                    return ErrDescription;
            }

            return null;    
        }

        ErrDescription? VisitWire(WireNode wireNode, int componentId, PowerState state)
        {
            ref Wire wire = ref _wires[wireNode.Id];

            if (wire.PowerState == state && wire.LastVisitComponentId == componentId)
                return null;

            if (wire.PowerState != state && wire.LastVisitComponentId != -1 && wire.LastVisitComponentId != componentId)
                return new ErrDescription(componentId, wire.LastVisitComponentId, state, wire.PowerState, wireNode.Id);

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
                        return CurrentShortCircuit = new ErrDescription(connectedWire.LastVisitComponentId, componentId, connectedWire.PowerState, state, wireNode.Id);
                }

                connectedWire.PowerState = state;

                // copy power state to other wires
                if(VisitWire(connection, componentId, state) is ErrDescription err)
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
                ref Wire wire = ref _wires[wireNode.Id];
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

    private void RecordDelayValues(GlobalStateTable state, ulong previousHash)
    {
        foreach (var delayComponentId in _delayComponentIds)
        {
            Component delayComponent = _components[delayComponentId];
            state[GlobalStateTable.CreateAddress(previousHash, delayComponent.Position)] = PowerStateAt(delayComponent.GetInputPosition(0));
        }
    }

    private FastStack<(Point A, Point B, int Id)> _wiresToMove = new(4);

    public bool MoveComponent(int componentId, Point to)
    {

        if (!CanMoveComponent(componentId, to))
            return false;

        MoveComponentCore(componentId, to);

        return true;
    }

    private void MoveComponentCore(int componentId, Point to)
    {
        ref Component component = ref _components[componentId];

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
    }

    public ref Wire Wire(int id) => ref _wires[id];

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
        Debug.WriteLine(id);
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

    private bool CanMoveComponent(int componentId, Point to, List<int>? exemptCollisons = null)
    {
        ref Component component = ref _components[componentId];

        if (!component.Exists)
            return false;
        if (component.Position == to)
            return true;

        foreach (var element in component.Blueprint.Display)
        {
            Point destPos = element.Offset + to;
            if (!InRange(destPos) || (
                this[destPos] is { Kind: not TileKind.Nothing } dest &&
                dest.ComponentId != componentId &&
                (!exemptCollisons?.Contains(componentId) ?? true))
                )
            {
                return false;
            }
        }

        return true;
    }

    public void MoveMany(List<int> componentIds, List<(int id, bool side)> wireIds, Point delta)
    {
        foreach(var (wireId, side) in wireIds)
        {
            if (!CanMoveWireNode(wireId, side, delta))
                return;
        }

        foreach(var componentId in componentIds)
        {
            if(!CanMoveComponent(componentId, _components[componentId].Position + delta, componentIds))
                return;
        }


        foreach (var (wireId, side) in wireIds)
        {
            MoveWireNode(wireId, side, delta);
        }

        foreach (var componentId in componentIds)
        {
            MoveComponentCore(componentId, _components[componentId].Position + delta);
        }

        bool CanMoveWireNode(int wireId, bool side, Point delta)
        {
            ref Wire wire = ref _wires[wireId];

            if (!wire.Exists)
                return false;

            ref Point pointToMove = ref side ? ref wire.A : ref wire.B;


            Point destination = pointToMove + delta;

            if (!InRange(destination))
                return false;

            Point otherSide = side ? wire.B : wire.A;

            if (pointToMove == destination)
                return false;
            if (destination == otherSide && !wireIds.Any(w => w.id == wireId && w.side != side))
                return false;
            if (this[destination].Kind is TileKind.Component &&
                !componentIds.Contains(this[destination].ComponentId))
                return false;

            return true;
        }

        bool MoveWireNode(int wireId, bool side, Point delta)
        {
            ref Wire wire = ref _wires[wireId];
            ref Point pointToMove = ref side ? ref wire.A : ref wire.B;
            Point destination = pointToMove + delta;
            Point otherSide = side ? wire.B : wire.A;

            ushort oldIndex = checked((ushort)(pointToMove.X + pointToMove.Y * Width));
            _wireMap[oldIndex].LazyInit().Remove(new WireNode() { Id = wireId, To = otherSide });
            ushort newIndex = checked((ushort)(destination.X + destination.Y * Width));
            _wireMap[newIndex].LazyInit().PushRef() = new WireNode() { Id = wireId, To = otherSide };
            pointToMove = destination;

            return true;
        }
    }

    public int CreateWire(Wire w)
    {
        if (w.A == w.B)
            return -1;
        if (!InRange(w.A) || !InRange(w.B))
            return -1;
        if (this[w.A] is { Kind: TileKind.Component } || this[w.B] is { Kind: TileKind.Component })
            return -1;
        ref var wire = ref _wires.Create(out int id);
        wire.A = w.A;
        wire.B = w.B;
        wire.WireKind = w.WireKind;

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

            var (color, outline) = wire.WireKind ?
                Constants.BundleWireColor :
                Constants.GetWireColor(wire.PowerState);

            DrawWire(g, Scale, wire, color, outline, wire.WireKind ? wire.PowerState.Values.ToString() : null);
        }

        if(CurrentShortCircuit is { WireId: >= 0 })
        {
            Wire w = _wires[CurrentShortCircuit.WireId];

            DrawWire(g, Scale, w, Color.Yellow, Color.DarkGoldenrod);
        }
    }

    public void DrawWire(Graphics g, float scale, Wire wire, Color color, Color outline, string? text = null)
    {
        var sb = g.ShapeBatch;

        var b = wire.B.ToVector2() * scale;
        var a = wire.A.ToVector2() * scale;

        sb.DrawLine(a,
                    b, Constants.WireRad, color, outline, 4);
        
        Node(wire.A, a);
        Node(wire.B, b);

        if(text is not null)
        {
            var center = (a + b) * 0.5f;
            g.DrawStringCentered(text, center);
        }

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