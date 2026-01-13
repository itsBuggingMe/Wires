using Frent;
using Frent.Components;
using Microsoft.Xna.Framework;
using System;

namespace Wires.Core.Sim.Components;

public struct Wire(Point a, Point b, WireKind kind) : IInitable, IDestroyable
{
    public Point A = a;
    public Point B = b;
    public readonly WireKind Kind = kind;
    public PowerState PowerState;
    public Entity LastVisitComponent;
    public Entity Self { get; private set; }

    public void Init(Entity self)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(A, B);
        World world = self.World;
        world.Create(new GridPositioned(A), new WireNode(self, B));
        world.Create(new GridPositioned(B), new WireNode(self, A));
        Self = self;
    }

    public readonly void Destroy()
    {
        Simulation simulation = Self.World.UniformProvider.GetUniform<Simulation>();
        simulation.CleanupWire(Self);
    }
}

public enum WireKind : byte
{
    Bit,
    Byte,
}