using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wires.Sim.Saving;

internal class LevelModel
{
    public int GridWidth { get; init; }
    public int GridHeight { get; init; }
    public required string Name { get; init; }
    public required ComponentModel[] Components { get; init; }
    public required ComponentTileModel[] ComponentTiles { get; init; }
    public required TestCaseModel[] TestCases { get; init; }
}

internal class ComponentTileModel
{
    public int X { get; init; }
    public int Y { get; init; }
    public required string TileKind { get; init; }
}

internal class TestCaseModel
{
    public required byte[] Inputs { get; init; }
    public required byte[] Outputs { get; init; }
}

internal class ComponentModel
{
    public required string BlueprintName { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public bool AllowDelete { get; init; }
    public int? InputOutputId { get; init; }
    public int Rotation { get; set; }
}