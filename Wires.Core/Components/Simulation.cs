using Apos.Shapes;
using Frent;
using Frent.Core;
using Frent.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Paper.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Wires.Core;
using Wires.Core.Components.Stateful;
using Wires.Core.Sim.Components;

namespace Wires.Core.Sim;

public class Simulation
{
    private readonly Tile[] _tiles;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public ref Tile this[int x, int y] => ref _tiles[x + y * Width];
    public ref Tile this[Point p] => ref this[p.X, p.Y];


    private readonly Queue<WorkItem> _workList = new Queue<WorkItem>(4);
    private readonly HashSet<Entity> _delayComponentIds = new(4);

    // coord -> wire node
    private ShortSparseSet<FastStack<Entity>> _wireMap = new();

    public int OutputCount { get; private set; }
    public int InputCount { get; private set; }

    public Query Wires => _entities.Query<Wire>();
    public Query Components => _entities.Query<ComponentData>();
    public List<SimulationComment> Comments { get; } = [];

    private Dictionary<Point, PowerState> _outputs = [];

    private readonly World _entities;


    public Simulation(int width = 24, int height = 24)
    {
        _ = checked((ushort)width * (ushort)height);

        _tiles = new Tile[width * height];

        Width = width;
        Height = height;

        _entities = new World(new DefaultUniformProvider().Add(this));
    }

    private const int MaxIterationCount = 10_000;

    public IEnumerable<TickResult> StepEnumerator(Blueprint blueprint, GlobalStateTable state, ulong previousAddressHash)
    {
        _outputs.Clear();

        _entities.Query<Wire>().Delegate((ref Wire s) =>
        {
            s.PowerState = PowerState.OffState;
            s.LastVisitComponent = Entity.Null;
        });

        yield return TickResult.SuccessInstance;

        // also add components
        TickResult initialError = TickResult.SuccessInstance;
        foreach (var tuple in _entities.Query<ComponentData>().EnumerateWithEntities<ComponentData>())
        {
            ref ComponentData componentData = ref tuple.Item1.Value;

            if (componentData.Blueprint.Custom is null)
            {
                if (componentData.Blueprint.Outputs.Length == 0) // skip updating items that are only sinks
                    continue;

                Point firstOutputPos = componentData.GetOutputPosition(0);

                PowerState? powerToApply = componentData.Blueprint.Descriptor switch
                {
                    Blueprint.IntrinsicBlueprint.On => PowerState.OnState,
                    Blueprint.IntrinsicBlueprint.Off => PowerState.OffState,
                    Blueprint.IntrinsicBlueprint.Input => blueprint.InputBuffer(componentData.InputOutputId),
                    Blueprint.IntrinsicBlueprint.Delay => state[GlobalStateTable.CreateAddress(previousAddressHash, componentData.Position)],
                    Blueprint.IntrinsicBlueprint.Switch => componentData.Blueprint.SwitchValue,
                    // investigate how I used to update dangling components
                    _ => null,  
                };

                if (powerToApply is { } p)
                {
                    if (StartVisit(firstOutputPos, tuple.Entity, p) is TickResult.Error error)
                    {
                        initialError = error;
                        break;
                    }
                }

                continue;
            }

            if (componentData.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, componentData.Position)) is TickResult.Error stepError)
            {
                initialError = new TickResult.InnerError(tuple.Entity, stepError);
                break;
            }

            for (int i = 0; i < componentData.Blueprint.Outputs.Length; i++)
            {
                PowerState power = componentData.Blueprint.OutputBuffer(i);
                Point outputPosition = componentData.GetOutputPosition(i);

                if (StartVisit(componentData.GetOutputPosition(i), tuple.Entity, power) is TickResult.Error error)
                {
                    initialError = error;
                    break;
                }
            }

            if (initialError is TickResult.Error)
                break;
        }

        if (initialError is TickResult.Error)
        {
            yield return initialError;
            yield break;
        }

        // do work

        while (_workList.TryDequeue(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            ref ComponentData component = ref tile.Meta.Get<ComponentData>();

            TickResult currentResult = TickResult.SuccessInstance;
            switch (component.Blueprint.Descriptor)
            {
                case Blueprint.IntrinsicBlueprint.Output:
                    // we dont always read outputs
                    blueprint.OutputBuffer(component.InputOutputId) = w.State;
                    break;
                case Blueprint.IntrinsicBlueprint.FullAdder:
                    PowerState ia = PowerStateAt(component.GetInputPosition(0));
                    PowerState ib = PowerStateAt(component.GetInputPosition(1));
                    PowerState cin = PowerStateAt(component.GetInputPosition(2));

                    if (StartVisit(component.GetOutputPosition(0), tile.Meta,
                        (ia.On ^ ib.On ^ cin.On) ? PowerState.OnState : PowerState.OffState) is TickResult.Error sumError)
                    {
                        currentResult = sumError;
                        break;
                    }

                    currentResult = StartVisit(component.GetOutputPosition(1), tile.Meta,
                        ((ia.On && ib.On) || ((ia.On ^ ib.On) && cin.On)) ? PowerState.OnState : PowerState.OffState);
                    break;
                case Blueprint.IntrinsicBlueprint.Splitter:
                    PowerState powerState = w.State;

                    for (int i = 0; i < 8; i++)
                    {
                        if (StartVisit(component.GetOutputPosition(i), tile.Meta, ((1 << i) & powerState.Values) != 0 ? PowerState.OnState : PowerState.OffState) is TickResult.Error error)
                        {
                            currentResult = error;
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

                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, outputState);
                    break;
                case Blueprint.IntrinsicBlueprint.RAM:
                    ushort combinedAddress = 
                        (ushort)(PowerStateAt(component.GetInputPosition(2)).Values << 8
                        | PowerStateAt(component.GetInputPosition(3)).Values);

                    var output = state.TickRam(
                        PowerStateAt(component.GetInputPosition(0)),
                        PowerStateAt(component.GetInputPosition(1)),
                        combinedAddress
                        );
                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, output);
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
                        Blueprint.IntrinsicBlueprint.AND =>    new((byte)(a.Values & b.Values)),
                        Blueprint.IntrinsicBlueprint.OR =>     new((byte)(a.Values | b.Values)),
                        Blueprint.IntrinsicBlueprint.XOR =>    new((byte)(a.Values ^ b.Values)),
                        Blueprint.IntrinsicBlueprint.NAND =>   !(a.On && b.On) ? PowerState.OnState : PowerState.OffState,
                        Blueprint.IntrinsicBlueprint.NOR =>    !(a.On || b.On) ? PowerState.OnState : PowerState.OffState,
                        Blueprint.IntrinsicBlueprint.XNOR =>   !(a.On ^ b.On) ? PowerState.OnState : PowerState.OffState,
                        _ => throw new UnreachableException()
                    };

                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, outputPowerState);
                    break;
                case Blueprint.IntrinsicBlueprint.NOT:
                    PowerState a1 = PowerStateAt(component.GetInputPosition(0));
                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, a1.On ? PowerState.OffState : PowerState.OnState);
                    break;
                case Blueprint.IntrinsicBlueprint.DEC8:
                    if(PowerStateAt(component.GetInputPosition(3)).Off)
                    {// not enabled
                        for (int i = 0; i < 8; i++)
                        {
                            if (StartVisit(component.GetOutputPosition(i), tile.Meta, PowerState.OffState) is TickResult.Error error)
                            {
                                currentResult = error;
                                break;
                            }
                        }

                        break;
                    }

                    int index = 
                        (PowerStateAt(component.GetInputPosition(0)).On ? 0b001 : 0) |
                        (PowerStateAt(component.GetInputPosition(1)).On ? 0b010 : 0) |
                        (PowerStateAt(component.GetInputPosition(2)).On ? 0b100 : 0);

                    for(int i = 0; i < 8; i++)
                    {
                        if (StartVisit(component.GetOutputPosition(i), tile.Meta, index == i ? PowerState.OnState : PowerState.OffState) is TickResult.Error error)
                        {
                            currentResult = error;
                            break;
                        }
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.None:
                    if (component.Blueprint.Custom is null)
                        goto default;
                    // custom
                    for(int i = 0; i < component.Blueprint.Inputs.Length; i++)
                        component.Blueprint.InputBuffer(i) = PowerStateAt(component.GetInputPosition(i));

                    component.Blueprint.OutputBufferRaw.AsSpan().Clear();

                    if(component.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, component.Position)) is TickResult.Error pow)
                    {
                        yield return new TickResult.InnerError(tile.Meta, pow);
                        yield break;
                    }

                    for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                    {
                        PowerState power = component.Blueprint.OutputBuffer(i);
                        Point outputPosition = component.GetOutputPosition(i);

                        if (StartVisit(outputPosition, tile.Meta, power) is TickResult.Error error)
                        {
                            currentResult = error;
                            break;
                        }
                    }
                    break;
                // once we hit a delay component, we stop processing further
                // the responsibility of updating the input state of delay components is elsewhere
                // reading the delay component is done as a seed component
                case Blueprint.IntrinsicBlueprint.Delay:
                    break;
                // display components dont have side effects in the simulation
                case Blueprint.IntrinsicBlueprint.Disp:
                    break;
                default: throw new NotImplementedException();
            }

            yield return currentResult;
        }

        RecordDelayValues(state, previousAddressHash);

        TickResult StartVisit(Point point, Entity component, PowerState state)
        {
            _outputs[point] = state;

            foreach (Entity connection in WiresAt(point))
            {
                if (VisitWire(connection.Get<WireNode>(), component, state) is TickResult.Error err)
                    return err;
            }

            return TickResult.SuccessInstance;    
        }

        TickResult VisitWire(WireNode wireNode, Entity component, PowerState state)
        {
            ref Wire wire = ref wireNode.Wire.Get<Wire>();

            if (wire.PowerState == state && wire.LastVisitComponent == component)
                return TickResult.SuccessInstance;

            if (wire.PowerState != state && wire.LastVisitComponent.IsAlive && wire.LastVisitComponent != component)
                return new TickResult.ShortCircuit(wireNode.Wire, component, wire.LastVisitComponent);

            wire.LastVisitComponent = component;
            wire.PowerState = state;

            if (this[wireNode.Destination].Kind is TileKind.Input)
                _workList.Enqueue(new WorkItem(wireNode.Destination, state));

            // handle connecting wires
            // this is simlar to recursive flood fill
            foreach (Entity connection in WiresAt(wireNode.Destination))
            {
                ref Wire connectedWire = ref connection.Get<WireNode>().Wire.Get<Wire>();

                if (connectedWire.LastVisitComponent.IsAlive)
                {// this wire has been powered already
                    if (connectedWire.LastVisitComponent == component)
                    {
                        if (connectedWire.PowerState == state)
                            continue;
                    }
                    else if (connectedWire.PowerState != state)
                        return new TickResult.ShortCircuit(wireNode.Wire, connectedWire.LastVisitComponent, component);
                }

                // copy power state to other wires
                if(VisitWire(connection.Get<WireNode>(), component, state) is TickResult.Error err)
                {
                    return err;
                }
            }

            return TickResult.SuccessInstance;
        }

        bool ConnectedToAnOutput(Point connection)
        {
            foreach(var wireNode in WiresAt(connection))
            {
                ref WireNode nodeRef = ref wireNode.Get<WireNode>();
                ref Wire wire = ref nodeRef.Wire.Get<Wire>();
                if (!wire.LastVisitComponent.IsAlive)
                    continue;

                if (this[nodeRef.Destination].Kind is TileKind.Output)
                    return true;

                if (ConnectedToAnOutput(nodeRef.Destination))
                    return true;
            }

            return false;
        }
    }

    private void RecordDelayValues(GlobalStateTable state, ulong previousHash)
    {
        foreach (var delayComponentId in _delayComponentIds)
        {
            ComponentData delayComponent = delayComponentId.Get<ComponentData>();
            state[GlobalStateTable.CreateAddress(previousHash, delayComponent.Position)] = PowerStateAt(delayComponent.GetInputPosition(0));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="component"></param>
    /// <param name="to"></param>
    /// <returns><see langword="true"/> if the component does end up at <paramref name="to"/>, <see langword="false"/> otherwise.</returns>
    public bool MoveComponent(Entity component, Point to)
    {
        ref ComponentData comp = ref component.Get<ComponentData>();
        if (comp.Position == to)
            return true;

        // validate
        foreach((Point offset, _) in comp.Blueprint.Display)
        {
            Point destination = to + offset;
            if (!InRange(destination))
                return false;

            if (this[destination].Kind is not TileKind.Nothing && this[destination].Meta != component)
                return false;
        }

        // clear
        foreach ((Point offset, _) in comp.Blueprint.Display)
        {
            this[comp.Position + offset] = default;
        }

        foreach ((Point offset, TileKind kind) in comp.Blueprint.Display)
        {
            this[to + offset] = new Tile { Kind = kind, Meta = component };
        }

        comp.Position = to;
        return true;
    }

    public bool MoveMany(IReadOnlyCollection<Entity> components, IReadOnlyCollection<(Entity Id, bool IsA)> wireNodes, Point delta)
    {
        if (delta == default)
            return true;

        HashSet<Entity> componentSet = components.Where(c => c.IsAlive).ToHashSet();
        Dictionary<Entity, (bool MoveA, bool MoveB)> wireMoves = [];

        foreach (var (wireId, isA) in wireNodes)
        {
            if (!wireId.IsAlive)
                continue;

            if (!wireMoves.TryGetValue(wireId, out var move))
                move = default;

            move = isA ? (true, move.MoveB) : (move.MoveA, true);
            wireMoves[wireId] = move;
        }

        foreach (Entity component in componentSet)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            foreach ((Point offset, _) in data.Blueprint.Display)
            {
                Point destination = data.Position + offset + delta;
                if (!InRange(destination))
                    return false;

                Tile destinationTile = this[destination];
                if (destinationTile.Kind is not TileKind.Nothing && !componentSet.Contains(destinationTile.Meta))
                    return false;
            }
        }

        foreach (var (wireId, move) in wireMoves)
        {
            ref Wire wire = ref wireId.Get<Wire>();
            Point destinationA = move.MoveA ? wire.A + delta : wire.A;
            Point destinationB = move.MoveB ? wire.B + delta : wire.B;

            if (!CanPlaceWireEndpoint(destinationA, componentSet) ||
                !CanPlaceWireEndpoint(destinationB, componentSet) ||
                destinationA == destinationB)
            {
                return false;
            }
        }

        foreach (Entity component in componentSet)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            foreach ((Point offset, _) in data.Blueprint.Display)
                this[data.Position + offset] = default;
        }

        foreach (Entity component in componentSet)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            data.Position += delta;

            foreach ((Point offset, TileKind kind) in data.Blueprint.Display)
                this[data.Position + offset] = new Tile { Kind = kind, Meta = component };
        }

        foreach (var (wireId, move) in wireMoves)
        {
            ref Wire wire = ref wireId.Get<Wire>();
            RemoveWireNodes(wire);

            if (move.MoveA)
                wire.A += delta;
            if (move.MoveB)
                wire.B += delta;

            RebuildWireNodes(wireId, ref wire);
        }

        return true;
    }

    public bool RotateMany(IReadOnlyCollection<Entity> components, IReadOnlyCollection<(Entity Id, bool IsA)> wireNodes)
    {
        HashSet<Entity> componentSet = components.Where(c => c.IsAlive).ToHashSet();
        Dictionary<Entity, (bool MoveA, bool MoveB)> wireMoves = [];

        foreach (var (wireId, isA) in wireNodes)
        {
            if (!wireId.IsAlive)
                continue;

            if (!wireMoves.TryGetValue(wireId, out var move))
                move = default;

            move = isA ? (true, move.MoveB) : (move.MoveA, true);
            wireMoves[wireId] = move;
        }

        if (componentSet.Count == 0 && wireMoves.Count == 0)
            return false;

        Point center = SelectionCenter(componentSet, wireMoves);
        Dictionary<Entity, (Point Position, Blueprint Blueprint)> componentRotations = [];
        Dictionary<Entity, (Point A, Point B)> wireRotations = [];
        HashSet<Point> occupiedDestinations = [];

        foreach (Entity component in componentSet)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            Blueprint rotatedBlueprint = data.Blueprint.Clone(data.Blueprint.Rotation + 1);
            Point rotatedPosition = RotateClockwise(data.Position, center);

            foreach ((Point offset, _) in rotatedBlueprint.Display)
            {
                Point destination = rotatedPosition + offset;
                if (!InRange(destination))
                    return false;

                Tile destinationTile = this[destination];
                if (destinationTile.Kind is not TileKind.Nothing && !componentSet.Contains(destinationTile.Meta))
                    return false;

                if (!occupiedDestinations.Add(destination))
                    return false;
            }

            componentRotations[component] = (rotatedPosition, rotatedBlueprint);
        }

        foreach (var (wireId, move) in wireMoves)
        {
            ref Wire wire = ref wireId.Get<Wire>();
            Point destinationA = move.MoveA ? RotateClockwise(wire.A, center) : wire.A;
            Point destinationB = move.MoveB ? RotateClockwise(wire.B, center) : wire.B;

            if (!CanPlaceWireEndpoint(destinationA, componentSet) ||
                !CanPlaceWireEndpoint(destinationB, componentSet) ||
                destinationA == destinationB)
            {
                return false;
            }

            wireRotations[wireId] = (destinationA, destinationB);
        }

        foreach (Entity component in componentSet)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            foreach ((Point offset, _) in data.Blueprint.Display)
                this[data.Position + offset] = default;
        }

        foreach (var (component, rotation) in componentRotations)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            data.Position = rotation.Position;
            data.Blueprint = rotation.Blueprint;

            foreach ((Point offset, TileKind kind) in data.Blueprint.Display)
                this[data.Position + offset] = new Tile { Kind = kind, Meta = component };
        }

        foreach (var (wireId, rotation) in wireRotations)
        {
            ref Wire wire = ref wireId.Get<Wire>();
            RemoveWireNodes(wire);
            wire.A = rotation.A;
            wire.B = rotation.B;
            RebuildWireNodes(wireId, ref wire);
        }

        return true;

        static Point RotateClockwise(Point point, Point center)
        {
            Point delta = point - center;
            return center + new Point(-delta.Y, delta.X);
        }
    }

    private static Point SelectionCenter(IReadOnlyCollection<Entity> components, Dictionary<Entity, (bool MoveA, bool MoveB)> wireMoves)
    {
        Point min = new(int.MaxValue, int.MaxValue);
        Point max = new(int.MinValue, int.MinValue);

        foreach (Entity component in components)
        {
            ref ComponentData data = ref component.Get<ComponentData>();
            foreach ((Point offset, _) in data.Blueprint.Display)
            {
                Point tile = data.Position + offset;
                Include(tile);
            }
        }

        foreach (var (wireId, move) in wireMoves)
        {
            ref Wire wire = ref wireId.Get<Wire>();
            if (move.MoveA)
                Include(wire.A);
            if (move.MoveB)
                Include(wire.B);
        }

        return (min + max) / new Point(2);

        void Include(Point point)
        {
            min = new Point(int.Min(min.X, point.X), int.Min(min.Y, point.Y));
            max = new Point(int.Max(max.X, point.X), int.Max(max.Y, point.Y));
        }
    }

    readonly record struct WorkItem(Point Position, PowerState State);

    public Span<Entity> WiresAt(Point position)
    {
        return _wireMap.TryGet((ushort)(position.X + position.Y * Width), out FastStack<Entity> v) ? v.AsSpan() : [];
    }

    public bool HasWiresAt(Point position) => WiresAt(position).Length != 0;

    public Entity WireNodeAt(Point position)
    {
        foreach(var wire in WiresAt(position))
        {
            return wire;
        }
        return Entity.Null;
    }

    public Entity WireAtPath(Vector2 tilePosition, float maxDistance = 0.5f)
    {
        Entity closestWire = Entity.Null;
        float closestDistanceSquared = maxDistance * maxDistance;

        foreach (var (entity, wireRef) in Wires.EnumerateWithEntities<Wire>())
        {
            ref Wire wire = ref wireRef.Value;
            Vector2 a = wire.A.ToVector2();
            Vector2 b = wire.B.ToVector2();
            Vector2 segment = b - a;
            float lengthSquared = segment.LengthSquared();

            if (lengthSquared == 0)
                continue;

            float t = Vector2.Dot(tilePosition - a, segment) / lengthSquared;
            t = MathHelper.Clamp(t, 0, 1);

            float distanceSquared = Vector2.DistanceSquared(tilePosition, a + segment * t);
            if (distanceSquared <= closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestWire = entity;
            }
        }

        return closestWire;
    }

    public PowerState PowerStateAt(Point position)
    {
        return WireNodeAt(position).TryGet(out Ref<WireNode> node) ? 
            node.Value.Wire.Get<Wire>().PowerState :
            _outputs.TryGetValue(position, out PowerState v) ?
            v :
            PowerState.OffState;
    }

    /// <summary>
    /// Places a component at the specified position and rotation.
    /// </summary>
    /// <returns><see cref="Entity.Null"/> when unable to place, or an entity reference to the component.</returns>
    public Entity Place(Blueprint blueprint, Point position, int rotation, bool allowDelete = true, int inputOutputId = 0, bool initalState = false)
    {
        blueprint = blueprint.Clone(rotation);

        if (blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Switch)
            blueprint.SwitchValue = initalState ? PowerState.OnState : PowerState.OffState;

        foreach ((Point offset, _) in blueprint.Display)
        {
            Point tilePosition = position + offset;
            if (!InRange(tilePosition) || this[tilePosition].Kind is not TileKind.Nothing)
                return Entity.Null;
        }

        Entity e = _entities.Create(
            new ComponentData(position, blueprint, inputOutputId, allowDelete, initalState),
            new GridPositioned(position));

        foreach ((Point offset, TileKind kind) in blueprint.Display)
        {
            this[position + offset].Kind = kind;
            this[position + offset].Meta = e;
        }

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Input })
            InputCount++;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Output })
            OutputCount++;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Delay })
            _delayComponentIds.Add(e);

        return e;
    }

    /// <summary>
    /// Cleans up the nodes related to a wire. Only called by Wire.cs
    /// </summary>
    public void CleanupWire(Entity wireEntity)
    {
        if (!wireEntity.IsAlive)
            return;

        ref Wire w = ref wireEntity.Get<Wire>();
        RemoveWireNodes(w);
    }

    public void CleanupComponent(Entity component)
    {
        ref ComponentData componentData = ref component.Get<ComponentData>();
        Blueprint blueprint = componentData.Blueprint;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Input })
            InputCount--;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Output })
            OutputCount--;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Delay })
            _delayComponentIds.Remove(component);

        foreach ((Point offset, _) in blueprint.Display)
        {
            this[componentData.Position + offset] = default;
        }
    }

    public bool InRange(Point pos)
    {
        return (uint)pos.X < (uint)Width && (uint)pos.Y < (uint)Height;
    }

    public Entity CreateWire(Wire w)
    {
        if (w.A == w.B)
            return Entity.Null;

        if (!CanPlaceWireEndpoint(w.A, new HashSet<Entity>()) || !CanPlaceWireEndpoint(w.B, new HashSet<Entity>()))
            return Entity.Null;

        return _entities.Create(w);
    }

    public bool SplitWireAt(Entity wireEntity, Point position)
    {
        if (!wireEntity.IsAlive || !InRange(position))
            return false;

        ref Wire wire = ref wireEntity.Get<Wire>();
        bool horizontal = wire.A.Y == wire.B.Y && position.Y == wire.A.Y;
        bool vertical = wire.A.X == wire.B.X && position.X == wire.A.X;

        if (!horizontal && !vertical)
            return false;

        if (position == wire.A || position == wire.B)
            return false;

        if (horizontal && !Between(position.X, wire.A.X, wire.B.X))
            return false;

        if (vertical && !Between(position.Y, wire.A.Y, wire.B.Y))
            return false;

        if (!CanPlaceWireEndpoint(position, new HashSet<Entity>()))
            return false;

        Wire first = new(wire.A, position, wire.Kind);
        Wire second = new(position, wire.B, wire.Kind);
        wireEntity.Delete();
        CreateWire(first);
        CreateWire(second);
        return true;

        static bool Between(int value, int a, int b)
        {
            return value > int.Min(a, b) && value < int.Max(a, b);
        }
    }

    public bool MergeWiresAt(Point position)
    {
        if (!InRange(position) || this[position].Kind is not TileKind.Nothing)
            return false;

        Span<Entity> nodes = WiresAt(position);
        if (nodes.Length != 2)
            return false;

        Entity firstWireEntity = nodes[0].Get<WireNode>().Wire;
        Entity secondWireEntity = nodes[1].Get<WireNode>().Wire;

        if (!firstWireEntity.IsAlive || !secondWireEntity.IsAlive || firstWireEntity == secondWireEntity)
            return false;

        ref Wire firstWire = ref firstWireEntity.Get<Wire>();
        ref Wire secondWire = ref secondWireEntity.Get<Wire>();

        if (firstWire.Kind != secondWire.Kind)
            return false;

        Point firstEndpoint = OtherEndpoint(firstWire, position);
        Point secondEndpoint = OtherEndpoint(secondWire, position);
        bool horizontal = firstEndpoint.Y == position.Y && secondEndpoint.Y == position.Y;
        bool vertical = firstEndpoint.X == position.X && secondEndpoint.X == position.X;

        if (firstEndpoint == secondEndpoint ||
            (!horizontal && !vertical) ||
            !CanPlaceWireEndpoint(firstEndpoint, new HashSet<Entity>()) ||
            !CanPlaceWireEndpoint(secondEndpoint, new HashSet<Entity>()))
        {
            return false;
        }

        Wire mergedWire = new(firstEndpoint, secondEndpoint, firstWire.Kind);
        firstWireEntity.Delete();
        secondWireEntity.Delete();
        CreateWire(mergedWire);
        return true;

        static Point OtherEndpoint(Wire wire, Point sharedEndpoint)
        {
            return wire.A == sharedEndpoint ? wire.B : wire.A;
        }
    }

    public void SetupWireNode(Point p, Entity node)
    {
        _wireMap[checked((ushort)(p.X + p.Y * Width))].LazyInit().PushRef() = node;
    }

    internal SimulationSnapshot CreateSnapshot()
    {
        return new SimulationSnapshot(
            ComponentEntities()
                .Select(e =>
                {
                    ComponentData data = e.Get<ComponentData>();
                    return new ComponentSnapshot(
                        data.Blueprint,
                        data.Position,
                        data.Blueprint.Rotation,
                        data.Deletable,
                        data.InputOutputId,
                        data.Blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Switch ? data.Blueprint.SwitchValue.On : false);
                })
                .ToArray(),
            WireEntities()
                .Select(e => e.Get<Wire>())
                .Select(w => new WireSnapshot(w.A, w.B, w.Kind))
                .ToArray(),
            Comments
                .Select(c => new CommentSnapshot(c.Position, c.Text))
                .ToArray());
    }

    internal void RestoreSnapshot(SimulationSnapshot snapshot)
    {
        Comments.Clear();

        foreach (Entity wire in WireEntities())
            if (wire.IsAlive)
                wire.Delete();

        foreach (Entity component in ComponentEntities())
            if (component.IsAlive)
                component.Delete();

        foreach (var component in snapshot.Components)
        {
            Place(component.Blueprint, component.Position, component.Rotation, component.AllowDelete, component.InputOutputId, component.SwitchState);
        }

        foreach (var wire in snapshot.Wires)
        {
            CreateWire(new Wire(wire.A, wire.B, wire.Kind));
        }

        foreach (var comment in snapshot.Comments)
        {
            Comments.Add(new SimulationComment(comment.Position, comment.Text));
        }
    }

    private bool CanPlaceWireEndpoint(Point position, IReadOnlySet<Entity> movingComponents)
    {
        if (!InRange(position))
            return false;

        Tile tile = this[position];
        return tile.Kind is not TileKind.Component || movingComponents.Contains(tile.Meta);
    }

    private Entity[] ComponentEntities()
    {
        List<Entity> entities = [];
        foreach (var entity in Components.EnumerateWithEntities())
            entities.Add(entity);
        return entities.ToArray();
    }

    private Entity[] WireEntities()
    {
        List<Entity> entities = [];
        foreach (var entity in Wires.EnumerateWithEntities())
            entities.Add(entity);
        return entities.ToArray();
    }

    private void RemoveWireNodes(Wire wire)
    {
        if (wire.ANode.IsAlive)
        {
            ushort aIndex = checked((ushort)(wire.A.X + wire.A.Y * Width));
            _wireMap[aIndex].LazyInit().Remove(wire.ANode);
            wire.ANode.Delete();
        }

        if (wire.BNode.IsAlive)
        {
            ushort bIndex = checked((ushort)(wire.B.X + wire.B.Y * Width));
            _wireMap[bIndex].LazyInit().Remove(wire.BNode);
            wire.BNode.Delete();
        }
    }

    private void RebuildWireNodes(Entity wireEntity, ref Wire wire)
    {
        wire.ANode = wireEntity.World.Create(new GridPositioned(wire.A), new WireNode(wireEntity, wire.B));
        wire.BNode = wireEntity.World.Create(new GridPositioned(wire.B), new WireNode(wireEntity, wire.A));
    }

    public void Draw(Graphics g, TickResult tickResult)
    {
        ShapeBatch sb = g.ShapeBatch;

        const float Scale = Constants.Scale;

        if(InputHelper.Down(Keys.P))
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

        Entity shortCircuitComponentId = tickResult is TickResult.ShortCircuit s ? s.ComponentA : default;

        int id = 0;
        foreach (var (entity, comp) in _entities.Query<ComponentData>()
            .EnumerateWithEntities<ComponentData>())
        {
            // TODO: refactor to system
            ref ComponentData data = ref comp.Value;
            data.Blueprint.Draw(g, this, data.Position, Scale, Constants.WireRad, entity == shortCircuitComponentId, comp.Value.InputOutputId);
            id++;
        }

        foreach(var comps in _entities.Query<Wire>()
            .Enumerate<Wire>())
        {
            ref Wire w = ref comps.Item1.Value;

            var (color, outline) = w.Kind is WireKind.Byte ?
                Constants.BundleWireColor :
                Constants.GetWireColor(w.PowerState);

            DrawWire(g, Scale, w, color, outline, w.Kind is WireKind.Byte ? w.PowerState.Values.ToString() : null);
        }

        if(tickResult is TickResult.ShortCircuit { Wire.IsAlive: true } e)
        {
            Wire w = e.Wire.Get<Wire>();

            DrawWire(g, Scale, w, Color.Yellow, Color.DarkGoldenrod);
        }
    }

    // Refactor to not take color and read from Wire instead
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
            //g.DrawStringCentered(point.ToString(), a);
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

public sealed class SimulationComment(Point position, string text = "")
{
    public Point Position { get; set; } = position;
    public string Text { get; set; } = text;
}

internal sealed record SimulationSnapshot(ComponentSnapshot[] Components, WireSnapshot[] Wires, CommentSnapshot[] Comments);

internal sealed record ComponentSnapshot(
    Blueprint Blueprint,
    Point Position,
    int Rotation,
    bool AllowDelete,
    int InputOutputId,
    bool SwitchState);

internal sealed record WireSnapshot(Point A, Point B, WireKind Kind);

internal sealed record CommentSnapshot(Point Position, string Text);
