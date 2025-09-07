using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Wires.Core;

namespace Wires.Sim;
public class Blueprint
{
    private record class BlueprintFlyweight(
        ImmutableArray<(Point Offset, TileKind Kind)> Display,
        ImmutableArray<Point> Outputs,
        ImmutableArray<Point> Inputs,
        IntrinsicBlueprint Descriptor,
        string Text,
        Simulation? Custom
    );

    private readonly BlueprintFlyweight _data;

    public ImmutableArray<(Point Offset, TileKind Kind)> Display => _data.Display;
    public ImmutableArray<Point> Outputs => _data.Outputs;
    public ImmutableArray<Point> Inputs => _data.Inputs;
    public IntrinsicBlueprint Descriptor => _data.Descriptor;
    public string Text => _data.Text;
    public Simulation? Custom => _data.Custom;

    public PowerState[] InputBufferRaw => _inputBuffer;
    private PowerState[] _inputBuffer;
    public PowerState[] OutputBufferRaw => _outputBuffer;
    private PowerState[] _outputBuffer;
    public PowerState DelayValue;

    public ref PowerState InputBuffer(int index) => ref MemoryHelper.GetValueOrResize(ref _inputBuffer, index);
    public ref PowerState OutputBuffer(int index) => ref MemoryHelper.GetValueOrResize(ref _outputBuffer, index);

    private Blueprint(BlueprintFlyweight data)
    {
        _data = data;

        if (data.Custom is not null)
        {
            _inputBuffer = new PowerState[data.Custom.InputCount];
            _outputBuffer = new PowerState[data.Custom.OutputCount];
        }
        else
        {
            _inputBuffer = new PowerState[data.Inputs.Length];
            _outputBuffer = new PowerState[data.Outputs.Length];
        }
    }

    public Blueprint(ImmutableArray<(Point Offset, TileKind Kind)> display, string text, IntrinsicBlueprint descriptor = IntrinsicBlueprint.None)
        : this(new BlueprintFlyweight(
            display,
            display.Where(d => d.Kind is TileKind.Output).Select(d => d.Offset).ToImmutableArray(),
            display.Where(d => d.Kind is TileKind.Input).Select(d => d.Offset).ToImmutableArray(),
            descriptor,
            text,
            null))
    { }

    public Blueprint(Simulation custom, string text, ImmutableArray<(Point Offset, TileKind Kind)> display)
        : this(new BlueprintFlyweight(
            display,
            display.Where(d => d.Kind is TileKind.Output).Select(d => d.Offset).ToImmutableArray(),
            display.Where(d => d.Kind is TileKind.Input).Select(d => d.Offset).ToImmutableArray(),
            IntrinsicBlueprint.None,
            text,
            custom))
    { }

    public Blueprint Clone() => new Blueprint(_data);

    public void Reset()
    {
        if (Custom is null)
            return;
        Custom.ClearAllDelayValues();
        StepStateful();
    }

    public void StepStateful()
    {
        if (Custom is null)
            return;

        var enumerator = Custom.StepEnumerator(this).GetEnumerator();
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
                case TileKind.Component:
                    g.ShapeBatch.FillRectangle(mapPos - new Vector2(scale) * 0.5f, new(scale), new Color(181, 14, 59) * opacity, 8);
                    break;
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

    public static readonly Blueprint Delay = new([
        (new Point(0, 0), TileKind.Component),
        (new Point(1, 0), TileKind.Output),
        (new Point(-1, 0), TileKind.Input),
    ], "Delay", IntrinsicBlueprint.Delay);

    public enum IntrinsicBlueprint
    {
        None,
        NAND,
        On,
        Off,
        Output,
        Input,
        Delay,
    }
}
