namespace Wires.Sim;

internal record struct ComponentEntry(Blueprint Blueprint, TestCases? TestCases = null)
{
    public Simulation? World => Blueprint.Custom;
    public string Name = Blueprint.Text;
}