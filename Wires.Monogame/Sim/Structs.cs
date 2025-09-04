using Microsoft.Xna.Framework;
using Wires.Core;

namespace Wires.Sim;

public struct Wire(Point a, Point b) : IFreeListId
{
    /// <summary>
    /// Also used in free list
    /// </summary>
    public Point A = a;
    public Point B = b;

    public PowerState PowerState;
    public bool Exists = true;

    public int FreeNext { get => A.X; set => A.X = value; }
    bool IFreeListId.Exists { set => Exists = value; }
    public int LastVisitComponentId;
    public int LastVisitId;
}

public struct WireNode
{
    public Point To;
    public int Id;
}

public record struct PowerState(byte Values, bool IsActive)
{
    public readonly bool On => IsActive && Values != 0;
    public readonly bool Off => IsActive && Values == 0;
    public readonly bool IsInactive => !IsActive;
    public readonly bool OnAt(int index) => (Values & (1 << index)) != 0;
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
    public int LastVisitId;

    public int FreeNext { get => Position.X; set => Position.X = value; }
    public bool Exists { set { } }

    public Point GetOutputPosition(int index) => Blueprint.Outputs[index] + Position;
    public Point GetInputPosition(int index) => Blueprint.Inputs[index] + Position;
}