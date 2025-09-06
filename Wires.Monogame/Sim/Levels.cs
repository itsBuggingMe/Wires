using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Wires.Sim;
internal static class Levels
{
    private static PowerState On => PowerState.OnState;
    private static PowerState Off => PowerState.OffState;

    public static IEnumerable<ComponentEntry> LoadLevels(List<ComponentEntry> existingEntries)
    {
        using Stream stream = File.OpenRead("levels.json");
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