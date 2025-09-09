using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wires.Sim.Saving;

internal class LevelModel
{
    public static LevelModel Example => new LevelModel
    {
        GridHeight = 2,
        GridWidth = 2,
        Name = "ex",
        Components = 
        [
            new ComponentModel
            {
                AllowDelete = true,
                BlueprintName = "Input",
                Rotation = 0,
                InputOutputId = 0,
                X = 0,
                Y = 0,
            }
        ],

        ComponentTiles =
        [
            new ComponentTileModel
            {
                TileKind = "Input",
                X = -1,
                Y = 0,
            },
            new ComponentTileModel
            {
                TileKind = "Output",
                X = 1,
                Y = 0,
            }
        ],

        TestCases =
        [
            new TestCaseModel
            {
                Inputs = [ 1 ],
                Outputs = [ 1 ],
            },
            new TestCaseModel
            {
                Inputs = [ 0 ],
                Outputs = [ 0 ],
            },
        ],
    };

    public int GridWidth { get; init; }
    public int GridHeight { get; init; }
    public required string Name { get; init; }
    public required ComponentModel[] Components { get; init; }
    public required ComponentTileModel[] ComponentTiles { get; init; }
    public required TestCaseModel[] TestCases { get; init; }
    public WireModel[]? Wires { get; init; }
}

internal class ComponentTileModel
{
    public int X { get; init; }
    public int Y { get; init; }
    public required string TileKind { get; init; }
}

internal class TestCaseModel
{
    public required int[] Inputs { get; init; }
    public required int[] Outputs { get; init; }
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

internal class WireModel
{
    public int AX { get; init; }
    public int AY { get; init; }
    public int BX { get; init; }
    public int BY { get; init; }
}