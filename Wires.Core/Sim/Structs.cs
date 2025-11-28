using Microsoft.Xna.Framework;
using System;
using Paper.Core;
using System.Diagnostics;

namespace Wires.Core.Sim;

public struct Wire(Point a, Point b) : IFreeListId
{
    /// <summary>
    /// Also used in free list
    /// </summary>
    public Point A = a;
    public Point B = b;
    public bool WireKind;

    public PowerState PowerState;
    public bool Exists = true;

    public int FreeNext { get => A.X; set => A.X = value; }
    bool IFreeListId.Exists { set => Exists = value; }
    public int LastVisitComponentId;
}

public struct WireNode
{
    public Point To;
    public int Id;
}

[DebuggerDisplay("{On ? \"On\" : \"Off\",nq}")]
public record struct PowerState(byte Values)
{
    public readonly bool On => Values != 0;
    public readonly bool Off => Values == 0;
    public readonly bool OnAt(int index) => (Values & (1 << index)) != 0;

    public static readonly PowerState OnState = new PowerState(1);
    public static readonly PowerState OffState = new PowerState(0);
}

public enum TileKind : ushort
{
    Nothing,
    Output,
    Input,
    Component,
}

public struct Tile
{
    public int ComponentId;
    public TileKind Kind;
}

public struct Component : IFreeListId
{
    public Point Position;
    public Blueprint Blueprint;
    public int InputOutputId;
    public bool AllowDelete;

    public bool Exists { get; set; }

    public int FreeNext { get => Position.X; set => Position.X = value; }

    public Point GetOutputPosition(int index) => Blueprint.Outputs[index] + Position;
    public Point GetInputPosition(int index) => Blueprint.Inputs[index] + Position;
}