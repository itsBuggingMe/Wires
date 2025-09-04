using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Wires.Core;
using static Wires.Sim.Blueprint;

namespace Wires.Sim;

public class Blueprint(ImmutableArray<(Point Offset, TileKind Kind)> display, IntrinsicBlueprint descriptor = IntrinsicBlueprint.None)
{
    public readonly ImmutableArray<(Point Offset, TileKind Kind)> Display = display;

    public readonly ImmutableArray<Point> Outputs = display.Where(d => d.Kind is TileKind.Output).Select(d => d.Offset).ToImmutableArray();
    public readonly ImmutableArray<Point> Inputs = display.Where(d => d.Kind is TileKind.Input).Select(d => d.Offset).ToImmutableArray();
    
    public readonly IntrinsicBlueprint Descriptor = descriptor;

    public static readonly Blueprint Transisstor = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(-1, 0), TileKind.Input),
        (new Point(0, -1), TileKind.Input),
        (new Point(1, 0), TileKind.Output),
        ], IntrinsicBlueprint.Transisstor);

    public static readonly Blueprint On = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        ], IntrinsicBlueprint.On);

    public static readonly Blueprint Off = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        ], IntrinsicBlueprint.Off);

    public static readonly Blueprint Not = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(-1, 0), TileKind.Input),
        (new Point(1, 0), TileKind.Output),
        ], IntrinsicBlueprint.Not);

    public static readonly Blueprint Output = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(-1, 0), TileKind.Output),
        ], IntrinsicBlueprint.Output);

    public enum IntrinsicBlueprint
    {
        None,
        Transisstor,
        Not,
        On,
        Off,
        Output,
    }
}