using Frent;
using Frent.Components;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace Wires.Core.Sim.Components;

internal  struct WireNode(Entity e, Point d) : IInitable
{
    public Entity Wire
    {
        get
        {
            Debug.Assert(e.IsAlive);
            return e;
        }
    }
    public readonly Point Destination = d;
    private Entity self;
    public void Init(Entity self)
    {
        this.self = self;
        self.World.UniformProvider.GetUniform<Simulation>()
            .SetupWireNode(self.Get<GridPositioned>().Position, self);

        Wire.OnDelete += e =>
        {
            if(self.IsAlive)
            {

            }
        };
    }
}
