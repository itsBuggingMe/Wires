using Microsoft.Xna.Framework;
using Paper.Core.UI;
using Wires.Core.Sim;

namespace Wires.Core.UI;

internal class ShortCircuitTooltip : BorderedElement
{
    private readonly ErrDescription _ErrDescription;
    private readonly Simulation _simulation;

    public ShortCircuitTooltip(UIVector2 pos, Simulation simulation, ErrDescription ErrDescription) : base(pos, new UIVector2(260, 150, false, false))
    {
        _ErrDescription = ErrDescription;
        _simulation = simulation;
        ElementAlign = Paper.Core.UI.ElementAlign.BottomLeft;
    }

    public override bool Update()
    {
        SetPosition(Graphics.Camera.WorldToScreen(_simulation.Wire(_ErrDescription.WireId).A.ToVector2() * Constants.Scale));
        return base.Update();
    }

    public override void Draw()
    {
        base.Draw();
        if (_ErrDescription.IsCircularDep)
            return;
        var bound = Bounds;
        ref Component a = ref _simulation.GetComponent(_ErrDescription.ComponentIdA);
        ref Component b = ref _simulation.GetComponent(_ErrDescription.ComponentIdB);
        Graphics.SpriteBatchText.DrawString(Graphics.Font, 
            $"Short Circuit!\nThis happens when two\noutputs power a wire\nwith different values.\n\n{a.Blueprint.Text} & {b.Blueprint.Text}\nin conflict!", 
            new Vector2(Constants.Padding) + bound.Location.ToVector2(), Color.White);
    }
}
