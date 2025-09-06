using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using Apos.Shapes;
using Microsoft.Xna.Framework;
using Wires.Core;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using static Wires.Sim.Blueprint;

namespace Wires.Sim;

public class Blueprint
{
    public readonly ImmutableArray<(Point Offset, TileKind Kind)> Display;

    public readonly ImmutableArray<Point> Outputs;
    public readonly ImmutableArray<Point> Inputs;
    
    public readonly IntrinsicBlueprint Descriptor;
    public readonly PowerState[] InputBuffer;
    public readonly PowerState[] OutputBuffer;

    public readonly string Text;

#nullable enable
    public readonly Simulation? Custom;

    private Blueprint(ImmutableArray<(Point Offset, TileKind Kind)> display, string text, IntrinsicBlueprint descriptor = IntrinsicBlueprint.None)
    {
        Text = text;
        Outputs = display.Where(d => d.Kind is TileKind.Output).Select(d => d.Offset).ToImmutableArray();
        Inputs = display.Where(d => d.Kind is TileKind.Input).Select(d => d.Offset).ToImmutableArray();
        OutputBuffer = new PowerState[Outputs.Length];
        InputBuffer = new PowerState[Inputs.Length];

        Display = display;
        Descriptor = descriptor;
    }

    public Blueprint(Simulation custom, string text, ImmutableArray<(Point Offset, TileKind Kind)> display) : this(display, text, IntrinsicBlueprint.None)
    {
        Custom = custom;
        InputBuffer = new PowerState[custom.InputCount];
        OutputBuffer = new PowerState[custom.OutputCount];
    }

    public void StepStateful()
    {
        if (Custom is null)
            return;
        var enumerator = Custom.StepEnumerator(InputBuffer, OutputBuffer).GetEnumerator();
        while (enumerator.MoveNext()) ;
    }

    public void Draw(Graphics g, Simulation? sim, Point pos, float scale, float wireRad, float opacity = 1f)
    {
        foreach ((Point offset, TileKind kind) in Display.AsSpan())
        {
            Point tilePos = pos + offset;
            Vector2 mapPos = tilePos.ToVector2() * scale;

            var (color, outline) = Constants.GetWireColor(sim?.PowerStateAt(tilePos) ?? PowerState.OffState);

            switch (kind)
            {
                case TileKind.Input: 
                    g.ShapeBatch.FillEquilateralTriangle(mapPos, wireRad * 0.7f, new Color(14, 92, 181) * opacity, 0, MathHelper.PiOver2);
                    g.ShapeBatch.DrawCircle(mapPos, wireRad * 0.5f, color, outline);
                    break;
                case TileKind.Output: 
                    g.ShapeBatch.FillRectangle(mapPos - new Vector2(wireRad), new(wireRad * 2), Color.Orange * opacity); 
                    g.ShapeBatch.DrawCircle(mapPos, wireRad * 0.5f, color, outline);
                    break;
                case TileKind.Component: g.ShapeBatch.FillRectangle(mapPos - new Vector2(scale) * 0.5f, new(scale), new Color(181, 14, 59) * opacity, 8); break;
            }
        }

        g.DrawStringCentered(Text, pos.ToVector2() * scale);
    }

    public static readonly Blueprint NAND = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(-1, 0), TileKind.Input),
        (new Point(0, -1), TileKind.Input),
        (new Point(1, 0), TileKind.Output),
        ], "NAND", IntrinsicBlueprint.NAND);

    public static readonly Blueprint On = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        ], nameof(On), IntrinsicBlueprint.On);

    public static readonly Blueprint Off = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        ], nameof(Off), IntrinsicBlueprint.Off);

    public static readonly Blueprint Output = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(-1, 0), TileKind.Input),
        ], "Out", IntrinsicBlueprint.Output);

    public static readonly Blueprint Input = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        ], "In", IntrinsicBlueprint.Input);

    public enum IntrinsicBlueprint
    {
        None,
        NAND,
        On,
        Off,
        Output,
        Input,
    }
}