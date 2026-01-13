using Microsoft.Xna.Framework;
using Paper.Core.UI;
using Wires.Core.Components.Stateful;
using Wires.Core.Sim;
using Wires.Core.Sim.Components;

namespace Wires.Core.UI;

internal class ShortCircuitTooltip : BorderedElement
{
    private readonly Simulation _simulation;
    private readonly TickResult.ShortCircuit _err;

    public ShortCircuitTooltip(UIVector2 pos, Simulation simulation, TickResult.ShortCircuit shortCiruit) : base(pos, new UIVector2(260, 150, false, false))
    {
        _err = shortCiruit;
        _simulation = simulation;
        ElementAlign = Paper.Core.UI.ElementAlign.BottomLeft;
    }

    public override bool Update()
    {
        SetPosition(Graphics.Camera.WorldToScreen(_err.Wire.Get<Wire>().A.ToVector2() * Constants.Scale));
        return base.Update();
    }

    public override void Draw()
    {
        base.Draw();

        var bound = Bounds;
        ref ComponentData a = ref _err.ComponentA.Get<ComponentData>();
        ref ComponentData b = ref _err.ComponentB.Get<ComponentData>();
        Graphics.SpriteBatchText.DrawString(Graphics.Font, 
            $"Short Circuit!\nThis happens when two\noutputs power a wire\nwith different values.\n\n{a.Blueprint.Text} & {b.Blueprint.Text}\nin conflict!", 
            new Vector2(Constants.Padding) + bound.Location.ToVector2(), Color.White);
    }
}
