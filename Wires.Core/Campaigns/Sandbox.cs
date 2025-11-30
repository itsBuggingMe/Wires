using Microsoft.Xna.Framework.Input;
using Paper.Core;
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using Wires.Core.Sim;
using Wires.Core.Sim.Saving;
using Wires.Core.States;
using Wires.Core.UI;
using Wires.States;

namespace Wires.Core.Campaigns;

internal class Sandbox : Campaign
{
    private readonly CampaignState _parent;
    private int _timeSinceLastAutoSave = 0;

    private static readonly ImmutableArray<Blueprint> Intrinsics = [
        Blueprint.On,
        Blueprint.Off,
        Blueprint.NOT,
        Blueprint.AND,
        Blueprint.NAND,
        Blueprint.OR,
        Blueprint.NOR,
        Blueprint.XOR,
        Blueprint.XNOR,
        Blueprint.Splitter,
        Blueprint.Joiner,
        Blueprint.Switch,
        Blueprint.FullAdder,
        Blueprint.Delay,
        Blueprint.DEC8,
        Blueprint.RAM,
    ];

    public Sandbox(ServiceContainer serviceContainer, CampaignState campaignState, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction) : base(nameof(Sandbox), serviceContainer, graphics, editorUI, simInteraction)
    {
        _parent = campaignState;
        editorUI.NewComponentButton.Clicked += p =>
        {
            var sm = serviceContainer.GetService<ScreenManager>();
            sm.SwitchScreen(new ComponentEditor(campaignState, graphics, sm));
        };

        ComponentEditorButtonVisible = true;
    }

    private void Update()
    {
        if(_timeSinceLastAutoSave++ > 60 * 10)
        {
            _timeSinceLastAutoSave = 0;
            Levels.SaveLocalBinaryData(Levels.SaveToUrlSafeBinary(LevelItems));
        }

        if (_parent is { EditorOutput.ResultModel: not null })
        {
            foreach(var entry in Levels.AddModelsToEntries(AddMenuItems, [_parent.EditorOutput.ResultModel]))
            {
                UI.PlaceableComponents.Add(entry);
                SetLevel(entry);
                break;
            }
        }

        _parent?.EditorOutput = null;

        if(Keys.LeftControl.Down() || Keys.RightControl.Down())
        {
            if (InputHelper.RisingEdge(Keys.O))
            {
                Levels.LoadLocalJsonData(s =>
                {
                    AddMenuItems.Clear();
                    UI.Levels.Clear();
                    LevelItems.Clear();
                    UI.PlaceableComponents.Clear();

                    foreach (var b in Intrinsics)
                        AddMenu(new ComponentEntry(b));

                    foreach (var entry in Levels.AddModelsToEntries(AddMenuItems, JsonSerializer.Deserialize<LevelModel[]>(s) ?? []))
                    {
                        UI.PlaceableComponents.Add(entry);
                        SetLevel(entry);
                    }
                });
            }

            if (InputHelper.RisingEdge(Keys.S))
            {
                Levels.SaveLocalJsonData(JsonSerializer.Serialize(Levels.EntriesToModels(LevelItems)));
                Levels.SaveLocalBinaryData(Levels.SaveToUrlSafeBinary(LevelItems));
            }
        }
        //if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.L))
        //{
        //    Levels.LoadLocalBinaryData(s =>
        //    {
        //        AddMenuItems.Clear();
        //        UI.PlaceableComponents.Clear();
        //
        //        foreach (var b in Intrinsics)
        //            AddMenu(new ComponentEntry(b));
        //
        //        foreach (var entry in Levels.LoadFromUrlSafeBinary(s, AddMenuItems))
        //        {
        //            AddMenu(entry);
        //            SetLevel(entry);
        //        }
        //    });
        //}
        //
        //if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.S))
        //{
        //    Levels.SaveLocalBinaryData(Levels.SaveToUrlSafeBinary(LevelItems));
        //}
    }

    protected override IEnumerable LevelLogic()
    {
        foreach (var b in Intrinsics)
            AddMenu(new ComponentEntry(b));

        SetLevel(new ComponentEntry(new Blueprint(new Simulation(128, 128), "Sandbox", [(default, TileKind.Component)]), new TestCases([])));

        UI.NewComponentButton.Visible = true;

        yield return null;

        Levels.LoadLocalBinaryData(s =>
        {
            AddMenuItems.Clear();
            UI.Levels.Clear();
            LevelItems.Clear();
            UI.PlaceableComponents.Clear();

            foreach (var b in Intrinsics)
                AddMenu(new ComponentEntry(b));

            foreach (var entry in Levels.LoadFromUrlSafeBinary(s, AddMenuItems))
            {
                AddMenu(entry);
                SetLevel(entry);
            }
        });

        while (true)
        {
            Update();
            yield return null;
        }
    }
}
