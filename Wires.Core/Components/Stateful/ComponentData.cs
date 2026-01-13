using Frent;
using Frent.Components;
using Microsoft.Xna.Framework;
using Wires.Core.Sim;

namespace Wires.Core.Components.Stateful;

internal struct ComponentData(Point pos, Blueprint blueprint, int inputOutputId, bool allowDelete, bool state = false) : IInitable, IDestroyable
{
    public Point Position = pos;
    public Blueprint Blueprint = blueprint;
    public int InputOutputId = inputOutputId;
    public bool Deletable = allowDelete;
    // for switches
    public bool State = state;
    public Entity Self;

    public Point GetOutputPosition(int index) => Blueprint.Outputs[index] + Position;
    public Point GetInputPosition(int index) => Blueprint.Inputs[index] + Position;

    public void Init(Entity self) => Self = self;

    public void Destroy()
    {
        Self.World.UniformProvider.GetUniform<Simulation>().CleanupComponent(Self);
    }
}