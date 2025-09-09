#if BLAZORGL
using Microsoft.JSInterop;
#endif
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Wires.Sim.Saving;

internal static class Levels
{
#if BLAZORGL
    public static IJSRuntime JSRuntimeInstance { get; set; } = null!;
#else
    const string Path = "save.txt";
#endif

    private static PowerState On => PowerState.OnState;
    private static PowerState Off => PowerState.OffState;


    public static string LoadLocalData()
    {
#if BLAZORGL
        return JSRuntimeInstance.InvokeAsync<string>("getClipboard").Result;
#else
        return File.Exists(Path) ? File.ReadAllText("save.txt") : string.Empty;
#endif
    }

    public static string SerializeComponentEntries(List<ComponentEntry> simulations)
    {
        LevelModel[] models = simulations.Where(c => c.Custom is not null)
            .Select(c => new LevelModel
            {
                GridWidth = c.Custom!.Width,
                GridHeight = c.Custom!.Height,
                Name = c.Name,
                Components = c.Custom!.Components
                    .Select(c => new ComponentModel
                    {
                        BlueprintName = c.Blueprint.Text,
                        X = c.Position.X,
                        Y = c.Position.Y,
                        AllowDelete = c.AllowDelete,
                        InputOutputId = c.InputOutputId,
                        Rotation = c.Blueprint.Rotation,
                    }).ToArray(),
                ComponentTiles = c.Blueprint
                    .Display
                    .Select(d => new ComponentTileModel
                    {
                        TileKind = d.Kind.ToString(),
                        X = d.Offset.X,
                        Y = d.Offset.Y,
                    })
                    .ToArray(),
                TestCases = c.TestCases?.Enumerable?
                    .Select(i => new TestCaseModel
                    {
                        Inputs = i.Input.Select(c => c.Values).ToArray(),
                        Outputs = i.Output.Select(c => c.Values).ToArray(),
                    }).ToArray() ?? [],
            })
            .ToArray();

        return JsonSerializer.Serialize(models);
    }

    public static void SaveLocalData(string s)
    {
#if BLAZORGL
        _ = JSRuntimeInstance.InvokeVoidAsync("setClipload", s);
#else
        File.WriteAllText(Path, s);
#endif
    }

    public static IEnumerable<ComponentEntry> LoadLevels(List<ComponentEntry> existingEntries)
    {
        using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(LevelsJson));
        LevelModel[] levels = JsonSerializer.Deserialize<LevelModel[]>(stream) ?? [];
        return CreateComponentEntriesFromModels(existingEntries, levels);
    }

    public static IEnumerable<ComponentEntry> CreateComponentEntriesFromModels(List<ComponentEntry> existingEntries, LevelModel[] levels)
    {
        return levels.Select(m =>
        {
            Simulation simulation = new Simulation(m.GridWidth, m.GridHeight);

            foreach (var component in m.Components)
            {
                simulation.Place(component.BlueprintName switch
                {
                    "Input" => Blueprint.Input,
                    "Output" => Blueprint.Output,
                    _ => existingEntries.FirstOrDefault(m => m.Name == component.BlueprintName)?.Blueprint ??
                        throw new System.Exception($"Could not find blueprint of name: {component.BlueprintName}")
                }, new(component.X, component.Y), component.Rotation, component.AllowDelete, component.InputOutputId ?? 0);
            }

            TestCases? testCases = m.TestCases.Length == 0 ? null :
                new TestCases(
                    m.TestCases.Select(t =>
                        (t.Inputs.Select(i => new PowerState(i)).ToArray(),
                        t.Outputs.Select(i => new PowerState(i)).ToArray())
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
                "Inputs": [ 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0 ],
                "Outputs": [ 1 ]
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
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": -1,
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
                "Inputs": [ 1, 1 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
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
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": -1,
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
                "Inputs": [ 1, 1 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
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
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": -1,
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
                "Inputs": [ 1, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 1 ]
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
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": -1,
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
                "Inputs": [ 1, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
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
                "Y": 1,
                "TileKind": "Input"
              },
              {
                "X": -1,
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
                "Inputs": [ 1, 1 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 1 ]
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
                "Inputs": [ 0 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 1 ],
                "Outputs": [ 0, 1 ]
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
                "Inputs": [ 0, 0, 0 ],
                "Outputs": [ 1, 0, 0, 0, 0, 0, 0, 0 ]
              },
              {
                "Inputs": [ 0, 0, 1 ],
                "Outputs": [ 0, 1, 0, 0, 0, 0, 0, 0 ]
              },
              {
                "Inputs": [ 0, 1, 0 ],
                "Outputs": [ 0, 0, 1, 0, 0, 0, 0, 0 ]
              },
              {
                "Inputs": [ 0, 1, 1 ],
                "Outputs": [ 0, 0, 0, 1, 0, 0, 0, 0 ]
              },
              {
                "Inputs": [ 1, 0, 0 ],
                "Outputs": [ 0, 0, 0, 0, 1, 0, 0, 0 ]
              },
              {
                "Inputs": [ 1, 0, 1 ],
                "Outputs": [ 0, 0, 0, 0, 0, 1, 0, 0 ]
              },
              {
                "Inputs": [ 1, 1, 0 ],
                "Outputs": [ 0, 0, 0, 0, 0, 0, 1, 0 ]
              },
              {
                "Inputs": [ 1, 1, 1 ],
                "Outputs": [ 0, 0, 0, 0, 0, 0, 0, 1 ]
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
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [],
                "Outputs": [ 0 ]
              }
            ]
          },
          {
            "GridWidth": 16,
            "GridHeight": 16,
            "Name": "MemoryCell",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 5,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 11,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 8,
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
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 1, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 1, 1 ],
                "Outputs": [ 0 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 1 ]
              },
              {
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0 ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "HalfAdder",
            "Components": [
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 3,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 5,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 3,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 5,
                "AllowDelete": false,
                "InputOutputId": 1
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
                "Inputs": [ 0, 0 ],
                "Outputs": [ 0, 0 ]
              },
              {
                "Inputs": [ 0, 1 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 1, 0 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 1, 1 ],
                "Outputs": [ 0, 1 ]
              }
            ]
          },
          {
            "GridWidth": 9,
            "GridHeight": 9,
            "Name": "FullAdder",
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
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 6,
                "AllowDelete": false,
                "InputOutputId": 2
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 3,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 8,
                "Y": 5,
                "AllowDelete": false,
                "InputOutputId": 1
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
                "Inputs": [ 0, 0, 0 ],
                "Outputs": [ 0, 0 ]
              },
              {
                "Inputs": [ 0, 0, 1 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 0, 1, 0 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 0, 1, 1 ],
                "Outputs": [ 0, 1 ]
              },
              {
                "Inputs": [ 1, 0, 0 ],
                "Outputs": [ 1, 0 ]
              },
              {
                "Inputs": [ 1, 0, 1 ],
                "Outputs": [ 0, 1 ]
              },
              {
                "Inputs": [ 1, 1, 0 ],
                "Outputs": [ 0, 1 ]
              },
              {
                "Inputs": [ 1, 1, 1 ],
                "Outputs": [ 1, 1 ]
              }
            ]
          },
          {
            "GridWidth": 16,
            "GridHeight": 16,
            "Name": "Adder4Bit",
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
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 6,
                "AllowDelete": false,
                "InputOutputId": 2
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 3
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 10,
                "AllowDelete": false,
                "InputOutputId": 4
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 12,
                "AllowDelete": false,
                "InputOutputId": 5
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 14,
                "AllowDelete": false,
                "InputOutputId": 6
              },
              {
                "BlueprintName": "Input",
                "X": 0,
                "Y": 15,
                "AllowDelete": false,
                "InputOutputId": 7
              },
              {
                "BlueprintName": "Input",
                "X": 2,
                "Y": 0,
                "AllowDelete": false,
                "InputOutputId": 8
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 2,
                "AllowDelete": false,
                "InputOutputId": 0
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 4,
                "AllowDelete": false,
                "InputOutputId": 1
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 6,
                "AllowDelete": false,
                "InputOutputId": 2
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 8,
                "AllowDelete": false,
                "InputOutputId": 3
              },
              {
                "BlueprintName": "Output",
                "X": 15,
                "Y": 10,
                "AllowDelete": false,
                "InputOutputId": 4
              }
            ],
            "ComponentTiles": [
              {
                "X": -1,
                "Y": -4,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": -3,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": -2,
                "TileKind": "Input"
              },
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
                "X": -1,
                "Y": 2,
                "TileKind": "Input"
              },
              {
                "X": -1,
                "Y": 3,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": -2,
                "TileKind": "Input"
              },
              {
                "X": 0,
                "Y": 0,
                "TileKind": "Component"
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
              }
            ],
            "TestCases": [
              {
                "Inputs": [ 0, 0, 0, 0, 0, 0, 0, 0, 0 ],
                "Outputs": [ 0, 0, 0, 0, 0 ]
              },
              {
                "Inputs": [ 1, 0, 0, 0, 1, 0, 0, 0, 0 ],
                "Outputs": [ 0, 1, 0, 0, 0 ]
              },
              {
                "Inputs": [ 1, 1, 1, 1, 1, 1, 1, 1, 0 ],
                "Outputs": [ 0, 1, 1, 1, 1 ]
              },
              {
                "Inputs": [ 0, 0, 0, 1, 0, 0, 0, 1, 0 ],
                "Outputs": [ 0, 0, 1, 0, 0 ]
              },
              {
                "Inputs": [ 1, 0, 1, 0, 1, 1, 0, 1, 1 ],
                "Outputs": [ 1, 0, 0, 0, 1 ]
              }
            ]
          },
          {
            "GridWidth": 128,
            "GridHeight": 128,
            "Name": "SND",
            "Components": [
            ],
            "ComponentTiles": [
            ],
            "TestCases": [
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              },
              {
                "Inputs": [],
                "Outputs": []
              }
            ]
          }
        ]
        """;


}