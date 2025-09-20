using Wires.Core.UI;

namespace Wires.Core.Sim;

internal class ComponentEntry(Blueprint blueprint, TestCases? testCases = null, TruthTableData? truthTable = null)
{
    public Simulation? Custom => blueprint.Custom;
    public Blueprint Blueprint => blueprint;
    public TestCases? TestCases => testCases;
    public TruthTableData? TruthTable => truthTable;
    public string Name = blueprint.Text;
    public int TestCaseIndex { get; set; }
}