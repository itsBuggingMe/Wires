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

        yield return TickResult.Success;

        // also add components
        TickResult? initalErr = null;
        foreach (var tuple in _entities.Query<ComponentData>().EnumerateWithEntities<ComponentData>())
        {
            ref ComponentData componentData = ref tuple.Item1.Value;

            if (componentData.Blueprint.Custom is null)
            {
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
                    TickResult result = StartVisit(firstOutputPos, tuple.Entity, p);
                    if (result is not TickResult.NoError)
                    {
                        initalErr = result;
                        break;
                    }
                }

                continue;
            }

            componentData.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, componentData.Position));

            for (int i = 0; i < componentData.Blueprint.Outputs.Length; i++)
            {
                PowerState power = componentData.Blueprint.OutputBuffer(i);
                Point outputPosition = componentData.GetOutputPosition(i);

                if (StartVisit(componentData.GetOutputPosition(i), tuple.Entity, power) is { } err)
                {
                    initalErr = new TickResult.InnerError(tuple.Entity, err);
                    break;
                }
            }
        }

        if (initalErr is not null)
        {
            yield return initalErr;
            yield break;
        }

        // do work

        while (_workList.TryDequeue(out WorkItem w))
        {
            // handle connecting input
            Tile tile = this[w.Position];
            ref ComponentData component = ref tile.Meta.Get<ComponentData>();

            TickResult? currentResult = null;
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

                    // sum
                    currentResult = 
                        StartVisit(component.GetOutputPosition(0), tile.Meta, 
                        (ia.On ^ ib.On ^ cin.On) ? PowerState.OnState : PowerState.OffState);
                    currentResult =
                        StartVisit(component.GetOutputPosition(1), tile.Meta,
                        ((ia.On && ib.On) || ((ia.On ^ ib.On) && cin.On)) ? PowerState.OnState : PowerState.OffState);
                    break;
                case Blueprint.IntrinsicBlueprint.Splitter:
                    PowerState powerState = w.State;

                    for (int i = 0; i < 8; i++)
                    {
                        if (StartVisit(component.GetOutputPosition(i), tile.Meta, ((1 << i) & powerState.Values) != 0 ? PowerState.OnState : PowerState.OffState) is TickResult e)
                        {
                            currentResult = e;

                            if (e is not TickResult.NoError)
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
                    var output = state.TickRam(
                        PowerStateAt(component.GetInputPosition(0)),
                        PowerStateAt(component.GetInputPosition(1)),
                        PowerStateAt(component.GetInputPosition(2)));
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
                        Blueprint.IntrinsicBlueprint.AND =>     (a.On & b.On),
                        Blueprint.IntrinsicBlueprint.NAND =>   !(a.On & b.On),
                        Blueprint.IntrinsicBlueprint.OR =>      (a.On | b.On),
                        Blueprint.IntrinsicBlueprint.NOR =>    !(a.On | b.On),
                        Blueprint.IntrinsicBlueprint.XOR =>     (a.On ^ b.On),
                        Blueprint.IntrinsicBlueprint.XNOR =>   !(a.On ^ b.On),
                        _ => throw new UnreachableException()
                    } ? PowerState.OnState : PowerState.OffState;

                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, outputPowerState);
                    break;
                case Blueprint.IntrinsicBlueprint.NOT:
                    PowerState a1 = PowerStateAt(component.GetInputPosition(0));
                    currentResult = StartVisit(component.GetOutputPosition(0), tile.Meta, a1.On ? PowerState.OffState : PowerState.OnState);
                    break;
                case Blueprint.IntrinsicBlueprint.DEC8:
                    int index = 
                        (PowerStateAt(component.GetInputPosition(0)).On ? 0b001 : 0) |
                        (PowerStateAt(component.GetInputPosition(1)).On ? 0b010 : 0) |
                        (PowerStateAt(component.GetInputPosition(2)).On ? 0b100 : 0);

                    for(int i = 0; i < 8; i++)
                    {
                        currentResult = StartVisit(component.GetOutputPosition(i), tile.Meta, index == i ? PowerState.OnState : PowerState.OffState);
                    }
                    break;
                case Blueprint.IntrinsicBlueprint.None:
                    if (component.Blueprint.Custom is null)
                        goto default;
                    // custom
                    for(int i = 0; i < component.Blueprint.Inputs.Length; i++)
                        component.Blueprint.InputBuffer(i) = PowerStateAt(component.GetInputPosition(i));

                    component.Blueprint.OutputBufferRaw.AsSpan().Clear();

                    if(component.Blueprint.StepStateful(state, GlobalStateTable.CreateAddress(previousAddressHash, component.Position)) is TickResult pow)
                    {
                        yield return new TickResult.InnerError(tile.Meta, pow);
                        yield break;
                    }

                    for (int i = 0; i < component.Blueprint.Outputs.Length; i++)
                    {
                        PowerState power = component.Blueprint.OutputBuffer(i);
                        Point outputPosition = component.GetOutputPosition(i);

                        currentResult = StartVisit(component.GetOutputPosition(i), tile.Meta, power);
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

            yield return currentResult ?? TickResult.Success;
        }

        RecordDelayValues(state, previousAddressHash);

        TickResult StartVisit(Point point, Entity component, PowerState state)
        {
            _outputs[point] = state;

            foreach (Entity connection in WiresAt(point))
            {
                if (VisitWire(connection.Get<WireNode>(), component, state) is TickResult err)
                    return err;
            }

            return TickResult.Success;    
        }

        TickResult? VisitWire(WireNode wireNode, Entity component, PowerState state)
        {
            ref Wire wire = ref wireNode.Wire.Get<Wire>();

            if (wire.PowerState == state && wire.LastVisitComponent == component)
                return null;

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

                // TODO: this was passed by value before
                // maybe bug
                connectedWire.PowerState = state;

                // copy power state to other wires
                if(VisitWire(connection.Get<WireNode>(), component, state) is TickResult err)
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
            if (this[to + offset].Kind is not TileKind.Nothing && this[to + offset].Meta != component)
                return false;
        }

        // clear
        foreach ((Point offset, _) in comp.Blueprint.Display)
        {
            this[comp.Position + offset].Kind = TileKind.Nothing;
        }

        foreach ((Point offset, TileKind kind) in comp.Blueprint.Display)
        {
            this[to + offset].Kind = kind;
        }

        return true;
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
        foreach ((Point offset, _) in blueprint.Display)
        {
            if (this[position + offset].Kind is not TileKind.Nothing)
                return Entity.Null;
        }

        Entity e = _entities.Create(
            new ComponentData(position, blueprint.Clone(rotation), inputOutputId, allowDelete, initalState),
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

        ushort aIndex = checked((ushort)(w.A.X + w.A.Y * Width));
        _wireMap[aIndex].LazyInit().Remove(w.ANode);

        ushort bIndex = checked((ushort)(w.B.X + w.B.Y * Width));
        _wireMap[bIndex].LazyInit().Remove(w.BNode);

        w.ANode.Delete();
        w.BNode.Delete();
    }

    public void CleanupComponent(Entity component)
    {
        Blueprint blueprint = component.Get<ComponentData>().Blueprint;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Input })
            InputCount++;

        if (blueprint is { Descriptor: Blueprint.IntrinsicBlueprint.Output })
            OutputCount++;

        foreach ((Point offset, _) in blueprint.Display)
        {
            this[offset].Kind = TileKind.Nothing;
            this[offset].Meta = default;
        }
    }

    public bool InRange(Point pos)
    {
        return (uint)pos.X < (uint)Width && (uint)pos.Y < (uint)Height;
    }

    public Entity CreateWire(Wire w)
    {

        return _entities.Create(w);
    }

    public void SetupWireNode(Point p, Entity node)
    {
        _wireMap[checked((ushort)(p.X + p.Y * Width))].LazyInit().PushRef() = node;
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