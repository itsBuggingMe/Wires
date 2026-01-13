using Frent;

namespace Wires.Core.Sim;

public enum TileKind : ushort
{
    Nothing,
    Output,
    Input,
    Component,
}

public struct Tile
{
    public Entity Meta;
    public TileKind Kind;
}