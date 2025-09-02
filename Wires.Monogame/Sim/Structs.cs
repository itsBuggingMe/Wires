using Microsoft.Xna.Framework;

namespace Wires.Sim;

public struct Wire(byte values, Point from, Point to)
{
    /// <summary>
    /// Also used in free list
    /// </summary>
    public Point From = from;
    public Point To = to;
    
    public bool IsInactive;
    public byte Value = values;
    public bool Exists = true;
}

public struct Node()
{
}

public enum NodeKind
{

}

public struct Tile
{
    public int OccupiedId;
}