using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Wires.Sim;
internal static class Levels
{
    private static PowerState On => PowerState.OnState;
    private static PowerState Off => PowerState.OffState;

    public static IEnumerable<ComponentEntry> LoadLevels(List<ComponentEntry> existingEntries)
    {
        using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(LevelsJson));
        LevelModel[] levels = JsonSerializer.Deserialize<LevelModel[]>(stream) ?? [];

            return levels.Select(m =>
            {
            Simulation simulation = new Simulation(m.GridWidth, m.GridHeight);

            foreach(var component in m.Components)
            {
                simulation.Place(component.BlueprintName switch
                {
                    "Input" => Blueprint.Input,
                    "Output" => Blueprint.Output,
                    _ => existingEntries.FirstOrDefault(m => m.Name == component.BlueprintName).Blueprint ?? 
                        throw new System.Exception($"Could not find blueprint of name: {component.BlueprintName}")
                }, new(component.X, component.Y), component.AllowDelete, component.InputOutputId ?? 0);
            }

            TestCases? testCases = m.TestCases.Length == 0 ? null : 
                new TestCases(
                    m.TestCases.Select(t => 
                        (t.Inputs.Select(i => i ? On : Off).ToArray(), 
                        t.Outputs.Select(i => i ? On : Off).ToArray())
                        ).ToArray());

            return new ComponentEntry(new Blueprint(simulation, m.Name,
                m.ComponentTiles.Select(t => (new Point(t.X, t.Y), t.TileKind switch
                {
                    nameof(TileKind.Input) => TileKind.Input,
                    nameof(TileKind.Output) => TileKind.Output,
                    nameof(TileKind.Component) => TileKind.Component,
                    _ => throw new System.Exception($"Unknown tile kind: {t.TileKind}")
                })).ToImmutableArray()),
                testCases);
        });
    }

    private const string LevelsJson =
        """
        [
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "NOT",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false ],
                "Outputs": [ true ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "AND",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ false ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "OR",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ false ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "NOR",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true, true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ true ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "XOR",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true, true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ false ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "XNOR",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ true, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ true ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "Decoder1to2",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 2,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 6,
                "AllowDelete": false,
                "InputOutputId": 1
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": -1,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 1,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ false ],
                "Outputs": [ true, false ]
              },
              {
                "Inputs": [ true ],
                "Outputs": [ false, true ]
              }
            ]
          },
          {
            "GridWidth": 16,
            "GridHeight": 17,
            "Name": "Decoder3to8",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 12,
                "AllowDelete": false,
                "InputOutputId": 2
              },
        
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 1,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 3,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 5,
                "AllowDelete": false,
                "InputOutputId": 2
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 7,
                "AllowDelete": false,
                "InputOutputId": 3
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 9,
                "AllowDelete": false,
                "InputOutputId": 4
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 11,
                "AllowDelete": false,
                "InputOutputId": 5
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 13,
                "AllowDelete": false,
                "InputOutputId": 6
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 15,
                "AllowDelete": false,
                "InputOutputId": 7
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": 0,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": -3,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": -2,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": -1,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 1,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 2,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 3,
                "TileKind": "Output"
              },
              {
                "X": 1,
                "Y": 4,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ false, false, false ],
                "Outputs": [ true, false, false, false, false, false, false, false ]
              },
              {
                "Inputs": [ false, false, true ],
                "Outputs": [ false, true, false, false, false, false, false, false ]
              },
              {
                "Inputs": [ false, true, false ],
                "Outputs": [ false, false, true, false, false, false, false, false ]
              },
              {
                "Inputs": [ false, true, true ],
                "Outputs": [ false, false, false, true, false, false, false, false ]
              },
              {
                "Inputs": [ true, false, false ],
                "Outputs": [ false, false, false, false, true, false, false, false ]
              },
              {
                "Inputs": [ true, false, true ],
                "Outputs": [ false, false, false, false, false, true, false, false ]
              },
              {
                "Inputs": [ true, true, false ],
                "Outputs": [ false, false, false, false, false, false, true, false ]
              },
              {
                "Inputs": [ true, true, true ],
                "Outputs": [ false, false, false, false, false, false, false, true ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "Toggler",
            "Components": [
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [],
                "Outputs": [ true ]
              },
              {
                "Inputs": [],
                "Outputs": [ false ]
              },
              {
                "Inputs": [],
                "Outputs": [ true ]
              },
              {
                "Inputs": [],
                "Outputs": [ false ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "MemoryCell",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 2,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 6,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 0
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": -1,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
              },
              {
                "X": 1,
                "Y": 0,
                "TileKind": "Output"
              }
            ],
            "TestCases": [
              {
                "Inputs": [ false, false ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ true, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, false ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ true ]
              },
              {
                "Inputs": [ true, false ],
                "Outputs": [ false ]
              },
              {
                "Inputs": [ false, true ],
                "Outputs": [ false ]
              }
            ]
          }
        ]
        """;
}

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
    public required bool[] Inputs { get; init; }
    public required bool[] Outputs { get; init; }
}

internal class ComponentModel
{
    public required string BlueprintName { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public bool AllowDelete { get; init; }
    public int? InputOutputId { get; init; }
}