using Microsoft.Xna.Framework;

namespace Wires.Core.Sim.Components;

internal struct GridPositioned(Point pos)
{
    public Point Position = pos;
}
