namespace Wires.Sim;

internal class ComponentEntry(Blueprint blueprint, TestCases? testCases = null)
{
    public Simulation? Custom => blueprint.Custom;
    public Blueprint Blueprint => blueprint;
    public TestCases? TestCases => testCases;
    public string Name = blueprint.Text;
    public int TestCaseIndex { get; set; }
}