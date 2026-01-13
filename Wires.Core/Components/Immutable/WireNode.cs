using Frent;
using Microsoft.Xna.Framework;

namespace Wires.Core.Sim.Components;

internal readonly struct WireNode(Entity e, Point d)
{
    public readonly Entity Wire = e;
    public readonly Point Destination = d;
}
