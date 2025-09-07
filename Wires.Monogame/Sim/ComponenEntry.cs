namespace Wires.Sim;

internal record struct ComponentEntry(Blueprint Blueprint, TestCases? TestCases = null)
{
    public Simulation? Custom => Blueprint.Custom;
    public string Name = Blueprint.Text;
}