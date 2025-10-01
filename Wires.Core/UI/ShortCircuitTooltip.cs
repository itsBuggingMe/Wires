using Microsoft.Xna.Framework;
using Paper.Core.UI;
using Wires.Core.Sim;

namespace Wires.Core.UI;

internal class ShortCircuitTooltip : BorderedElement
{
    private readonly ShortCircuitDescription _shortCircuitDescription;
    private readonly Simulation _simulation;

    public ShortCircuitTooltip(UIVector2 pos, Simulation simulation, ShortCircuitDescription shortCircuitDescription) : base(pos, new UIVector2(260, 150, false, false))
    {
        _shortCircuitDescription = shortCircuitDescription;
        _simulation = simulation;
        ElementAlign = Paper.Core.UI.ElementAlign.BottomLeft;
    }

    public override bool Update()
    {
        SetPosition(Graphics.Camera.WorldToScreen(_simulation.Wire(_shortCircuitDescription.WireId).A.ToVector2() * Constants.Scale));
        return base.Update();
    }

    public override void Draw()
    {
        base.Draw();
        var bound = Bounds;
        ref Component a = ref _simulation.GetComponent(_shortCircuitDescription.ComponentIdA);
        ref Component b = ref _simulation.GetComponent(_shortCircuitDescription.ComponentIdB);
        Graphics.SpriteBatchText.DrawString(Graphics.Font, 
            $"Short Circuit!\nThis happens when two\noutputs power a wire\nwith different values.\n\n{a.Blueprint.Text} & {b.Blueprint.Text}\nin conflict!", 
            new Vector2(Constants.Padding) + bound.Location.ToVector2(), Color.White);
    }
}
