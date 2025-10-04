#if BLAZORGL
using Microsoft.JSInterop;
#endif
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Wires.Core.Sim.Saving;

#if BLAZORGL
public static class FileUtils
{
    public static Action<string>? OnFileLoad;
    public static Action<string>? OnCookieLoad;

    [JSInvokable]
    public static void FileFound(string data)
    {
        OnFileLoad?.Invoke(data);
        OnFileLoad = null;
    }

    [JSInvokable]
    public static void CookieFound(string data)
    {
        OnCookieLoad?.Invoke(data);
        OnCookieLoad = null;
    }
}
#endif

internal static class Levels
{
#if BLAZORGL
    public static IJSRuntime JSRuntimeInstance { get; set; } = null!;

#else
    const string Path = "save.json";
    const string PathBinary = "save.bin";
#endif

    private static PowerState On => PowerState.OnState;
    private static PowerState Off => PowerState.OffState;



    public static void LoadLocalJsonData(Action<string> onLoad)
    {
#if BLAZORGL
        FileUtils.OnFileLoad = onLoad;
        JSRuntimeInstance.InvokeAsync<string>("openFile");
#else
        onLoad(File.Exists(Path) ? File.ReadAllText(Path) : string.Empty);
#endif
    }

    public static void SaveLocalJsonData(string s)
    {
#if BLAZORGL
        JSRuntimeInstance.InvokeVoidAsync("saveFile", s);
#else
        File.WriteAllText(Path, s);
#endif
    }

    public static void LoadLocalBinaryData(Action<string> onLoad)
    {
#if BLAZORGL
        FileUtils.OnCookieLoad = onLoad;
        JSRuntimeInstance.InvokeAsync<string>("getStorage", "save");
#else
        onLoad(File.Exists(PathBinary) ? File.ReadAllText(PathBinary) : string.Empty);
#endif
    }

    public static void SaveLocalBinaryData(string urlSafeText)
    {
#if BLAZORGL
        JSRuntimeInstance.InvokeVoidAsync("setStorage", "save", urlSafeText);
#else
        File.WriteAllText(PathBinary, urlSafeText);
#endif
    }

    public static string SaveToUrlSafeBinary(List<ComponentEntry> componentEntries)
    {
        LevelModel[] models = EntriesToModels(componentEntries);
        using var ms = new MemoryStream();
        BinaryFormatModels(ms, models);
        return HttpUtility.UrlEncode(ms.ToArray());
    }

    public static IEnumerable<ComponentEntry> LoadFromUrlSafeBinary(string data, List<ComponentEntry> existingEntries)
    {
        byte[] bytes = HttpUtility.UrlDecodeToBytes(data);
        using var ms = new MemoryStream(bytes);
        var models = ReadBinaryFormatModels(ms);
        return ModelsToEntries(existingEntries, models);
    }

    public static string SaveToJson(List<ComponentEntry> simulations)
    {
        var models = EntriesToModels(simulations);
        return JsonSerializer.Serialize(models);
    }

    public static void LoadFromJson(List<ComponentEntry> existingEntries, string json)
    {
        var models = JsonSerializer.Deserialize<LevelModel[]>(json);
        existingEntries.AddRange(ModelsToEntries(existingEntries, models ?? []));
    }

    public static void BinaryFormatModels(Stream outputStream, LevelModel[] models)
    {
        BinaryWriter br = new BinaryWriter(outputStream);
        br.Write7BitEncodedInt(models.Length);
        foreach(var model in models)
        {
            br.Write7BitEncodedInt(model.GridWidth);
            br.Write7BitEncodedInt(model.GridHeight);
            br.Write(model.Name);
            
            br.Write7BitEncodedInt(model.Components.Length);
            foreach(var component in model.Components)
            {
                br.Write(component.BlueprintName);
                br.Write7BitEncodedInt(component.X);
                br.Write7BitEncodedInt(component.Y);
                br.Write(component.AllowDelete);
                br.Write7BitEncodedInt(component.InputOutputId ?? -1);
                br.Write(component.SwcState ?? false);
                br.Write7BitEncodedInt(component.Rotation);
            }
            
            br.Write7BitEncodedInt(model.ComponentTiles.Length);
            foreach(var tile in model.ComponentTiles)
            {
                br.Write7BitEncodedInt(tile.X);
                br.Write7BitEncodedInt(tile.Y);
                br.Write(tile.TileKind);
            }
            
            br.Write7BitEncodedInt(model.TestCases.Length);
            foreach(var testCase in model.TestCases)
            {
                br.Write7BitEncodedInt(testCase.Inputs.Length);
                foreach(var i in testCase.Inputs)
                    br.Write7BitEncodedInt(i);
                br.Write7BitEncodedInt(testCase.Outputs.Length);
                foreach(var i in testCase.Outputs)
                    br.Write7BitEncodedInt(i);
            }
            
            br.Write7BitEncodedInt(model.Wires?.Length ?? 0);
            foreach(var wireModel in (model.Wires ?? []))
            {
                br.Write7BitEncodedInt(wireModel.AX);
                br.Write7BitEncodedInt(wireModel.AY);
                br.Write7BitEncodedInt(wireModel.BX);
                br.Write7BitEncodedInt(wireModel.BY);
            }
        }
    }

    public static LevelModel[] ReadBinaryFormatModels(Stream inputStream)
    {
        using var br = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

        int modelCount = br.Read7BitEncodedInt();
        var models = new LevelModel[modelCount];

        for (int m = 0; m < modelCount; m++)
        {
            var gridWidth = br.Read7BitEncodedInt();
            var gridHeight = br.Read7BitEncodedInt();
            var name = br.ReadString();

            int componentCount = br.Read7BitEncodedInt();
            var components = new ComponentModel[componentCount];
            for (int c = 0; c < componentCount; c++)
            {
                components[c] = new ComponentModel
                {
                    BlueprintName = br.ReadString(),
                    X = br.Read7BitEncodedInt(),
                    Y = br.Read7BitEncodedInt(),
                    AllowDelete = br.ReadBoolean(),
                    InputOutputId = (br.Read7BitEncodedInt() is int inputOutputId && inputOutputId != -1 ? inputOutputId : null),
                    SwcState = br.ReadBoolean(),
                    Rotation = br.Read7BitEncodedInt()
                };
            }

            int tileCount = br.Read7BitEncodedInt();
            var tiles = new ComponentTileModel[tileCount];
            for (int t = 0; t < tileCount; t++)
            {
                var x = br.Read7BitEncodedInt();
                var y = br.Read7BitEncodedInt();
                var kind = br.ReadString();

                tiles[t] = new ComponentTileModel
                {
                    X = x,
                    Y = y,
                    TileKind = kind
                };
            }

            int testCount = br.Read7BitEncodedInt();
            var tests = new TestCaseModel[testCount];
            for (int tc = 0; tc < testCount; tc++)
            {
                int inputCount = br.Read7BitEncodedInt();
                var inputs = new int[inputCount];
                for (int i = 0; i < inputCount; i++)
                    inputs[i] = br.Read7BitEncodedInt();

                int outputCount = br.Read7BitEncodedInt();
                var outputs = new int[outputCount];
                for (int i = 0; i < outputCount; i++)
                    outputs[i] = br.Read7BitEncodedInt();

                tests[tc] = new TestCaseModel
                {
                    Inputs = inputs,
                    Outputs = outputs
                };
            }

            int wireCount = br.Read7BitEncodedInt();
            var wires = new WireModel[wireCount];
            for (int w = 0; w < wireCount; w++)
            {
                wires[w] = new WireModel
                {
                    AX = br.Read7BitEncodedInt(),
                    AY = br.Read7BitEncodedInt(),
                    BX = br.Read7BitEncodedInt(),
                    BY = br.Read7BitEncodedInt()
                };
            }

            models[m] = new LevelModel
            {
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                Name = name,
                Components = components,
                ComponentTiles = tiles,
                TestCases = tests,
                Wires = wires
            };
        }

        return models;
    }

    public static IEnumerable<ComponentEntry> ModelsToEntries(List<ComponentEntry> existingEntries, LevelModel[] levels)
    {
        return levels.Select(m =>
        {
            if (existingEntries.Any(e => e.Name == m.Name))
                return null;

            Simulation simulation = new Simulation(m.GridWidth, m.GridHeight);

            foreach (var component in m.Components)
            {
                simulation.Place(component.BlueprintName switch
                {
                    nameof(Blueprint.Input) => Blueprint.Input,
                    nameof(Blueprint.Output) => Blueprint.Output,
                    _ => existingEntries.FirstOrDefault(m => m.Name == component.BlueprintName)?.Blueprint ??
                        throw new System.Exception($"Could not find blueprint of name: {component.BlueprintName}")
                }, new(component.X, component.Y), component.Rotation, component.AllowDelete, component.InputOutputId ?? 0, component.SwcState ?? false);
            }

            foreach (var wire in m.Wires ?? [])
            {
                simulation.CreateWire(new Wire(new(wire.AX, wire.AY), new(wire.BX, wire.BY)));
            }

            TestCases? testCases = m.TestCases.Length == 0 ? new TestCases([]) :
                new TestCases(
                    m.TestCases.Select(t =>
                        (t.Inputs.Select(i => new PowerState((byte)i)).ToArray(),
                        t.Outputs.Select(i => new PowerState((byte)i)).ToArray())
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
        }).Where(n => n is not null)!;
    }

    public static LevelModel[] EntriesToModels(List<ComponentEntry> simulations)
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
                        SwcState = c.Blueprint.Descriptor is Blueprint.IntrinsicBlueprint.Switch ?
                            c.Blueprint.SwitchValue.On :
                            null,
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
                        Inputs = i.Input.Select(c => (int)c.Values).ToArray(),
                        Outputs = i.Output.Select(c => (int)c.Values).ToArray(),
                    }).ToArray() ?? [],
                Wires = c.Custom!.Wires
                    .Select(w => new WireModel
                    {
                        AX = w.A.X,
                        AY = w.A.Y,
                        BX = w.B.X,
                        BY = w.B.Y,
                    })
                    .ToArray(),
            })
            .ToArray();

        return models;
    }

    public static IEnumerable<ComponentEntry> LoadLevels(List<ComponentEntry> existingEntries) => throw new NotSupportedException();
}
