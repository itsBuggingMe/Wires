using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Wires.Core;

namespace Wires.Sim;
public class Blueprint
{
    [InlineArray(4)]
    private struct InlineArray4<T>
    {
        private T _0;
    }

    private record class BlueprintFlyweight(
        InlineArray4<ImmutableArray<(Point Offset, TileKind Kind)>> Displays,
        InlineArray4<ImmutableArray<Point>> Outputs,
        InlineArray4<ImmutableArray<Point>> Inputs,
        IntrinsicBlueprint Descriptor,
        string Text,
        Simulation? Custom
    );

    private readonly BlueprintFlyweight _data;

    public ImmutableArray<(Point Offset, TileKind Kind)> Display => _data.Displays[Rotation & 3];
    public ImmutableArray<Point> Outputs => _data.Outputs[Rotation & 3];
    public ImmutableArray<Point> Inputs => _data.Inputs[Rotation & 3];
    public IntrinsicBlueprint Descriptor => _data.Descriptor;
    public string Text => _data.Text;
    public Simulation? Custom => _data.Custom;

    public PowerState[] InputBufferRaw => _inputBuffer;
    private PowerState[] _inputBuffer;
    public PowerState[] OutputBufferRaw => _outputBuffer;
    private PowerState[] _outputBuffer;
    public PowerState DelayValue;
    public readonly int Rotation;

    public ref PowerState InputBuffer(int index) => ref MemoryHelper.GetValueOrResize(ref _inputBuffer, index);
    public ref PowerState OutputBuffer(int index) => ref MemoryHelper.GetValueOrResize(ref _outputBuffer, index);

    private Blueprint(BlueprintFlyweight data, int rotation)
    {
        _data = data;
        Rotation = rotation;

        if (data.Custom is not null)
        {
            _inputBuffer = new PowerState[data.Custom.InputCount];
            _outputBuffer = new PowerState[data.Custom.OutputCount];
        }
        else
        {
            _inputBuffer = new PowerState[Inputs.Length];
            _outputBuffer = new PowerState[Outputs.Length];
        }
    }

    private static Point Rotate(Point p, int rot) => rot switch
    {
        0 => p,
        1 => new Point(-p.Y, p.X),  // 90
        2 => new Point(-p.X, -p.Y), // 180
        3 => new Point(p.Y, -p.X),  // 270
        _ => p
    };

    public Blueprint(ImmutableArray<(Point Offset, TileKind Kind)> display, string text, IntrinsicBlueprint descriptor = IntrinsicBlueprint.None)
        : this(CreateFlyweight(display, descriptor, text, null), 0)
    { }

    public Blueprint(Simulation custom, string text, ImmutableArray<(Point Offset, TileKind Kind)> display)
        : this(CreateFlyweight(display, IntrinsicBlueprint.None, text, custom), 0)
    { }

    private static BlueprintFlyweight CreateFlyweight(ImmutableArray<(Point Offset, TileKind Kind)> display, IntrinsicBlueprint descriptor, string text, Simulation? custom)
    {
        InlineArray4<ImmutableArray<(Point Offset, TileKind Kind)>> displays = default;
        InlineArray4<ImmutableArray<Point>> outputs = default;
        InlineArray4<ImmutableArray<Point>> inputs = default;

        for (int r = 0; r < 4; r++)
        {
            var rotatedDisplay = display
                .Select(d => (Rotate(d.Offset, r), d.Kind))
                .ToImmutableArray();

            displays[r] = rotatedDisplay;
            outputs[r] = rotatedDisplay.Where(d => d.Kind == TileKind.Output).Select(d => d.Item1).ToImmutableArray();
            inputs[r] = rotatedDisplay.Where(d => d.Kind == TileKind.Input).Select(d => d.Item1).ToImmutableArray();
        }

        return new BlueprintFlyweight(displays, outputs, inputs, descriptor, text, custom);
    }

    public Blueprint Clone(int rotation) => new Blueprint(_data, rotation);

    public void Reset()
    {
        if (Custom is null)
            return;
        Custom.ClearAllDelayValues();
        StepStateful();
    }

    public ShortCircuitDescription? StepStateful(bool recordDelayValue = true)
    {
        if (Custom is null)
            return null;

        var enumerator = Custom.StepEnumerator(this).GetEnumerator();
        while (enumerator.MoveNext())
        {
            if(enumerator.Current is ShortCircuitDescription err)
                return err;
        }

        if(recordDelayValue)
        {
            Custom.RecordDelayValues();
        }

        return null;
    }

    public void Draw(Graphics g, Simulation? sim, Point pos, float scale, float wireRad, bool isShortCircuit, float opacity = 1f, int? rotationOverride = null)
    {
        Color? ssColor = isShortCircuit ? Color.DarkGoldenrod : null;

        foreach ((Point offset, TileKind kind) in _data.Displays[(rotationOverride ?? Rotation) & 3].AsSpan())
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
                    g.ShapeBatch.FillRectangle(mapPos - new Vector2(scale) * 0.5f, new(scale), ssColor ?? new Color(181, 14, 59) * opacity, 8);
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
