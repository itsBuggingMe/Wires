using Microsoft.Xna.Framework;

namespace Wires.Core.Sim.Components;

internal readonly struct GridPositioned(Point pos)
{
    public readonly Point Position = pos;
}
