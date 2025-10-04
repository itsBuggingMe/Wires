using Paper.Core;
using System.Collections;
using Wires.Core.Sim;
using Wires.Core.Sim.Saving;
using Wires.Core.States;
using Wires.Core.UI;
using Wires.States;
using Microsoft.Xna.Framework.Input;
using System.Text.Json;
using System.Collections.Immutable;

namespace Wires.Core.Campaigns;

internal class Sandbox : Campaign
{
    private readonly CampaignState _parent;

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
        Blueprint.Switch,
        Blueprint.Delay
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
        if(_parent is { EditorOutput.ResultModel: not null })
        {
            foreach(var entry in Levels.ModelsToEntries(AddMenuItems, [_parent.EditorOutput.ResultModel]))
            {
                AddMenu(entry);
                SetLevel(entry);
                break;
            }
        }

        _parent?.EditorOutput = null;

        if(Keys.LeftShift.Down())
        {
            if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.L))
            {
                Levels.LoadLocalJsonData(s =>
                {
                    AddMenuItems.Clear();
                    UI.PlaceableComponents.Clear();

                    foreach (var b in Intrinsics)
                        AddMenu(new ComponentEntry(b));

                    foreach (var entry in Levels.ModelsToEntries(AddMenuItems, JsonSerializer.Deserialize<LevelModel[]>(s) ?? []))
                    {
                        AddMenu(entry);
                        SetLevel(entry);
                    }
                });
            }

            if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.S))
            {
                Levels.SaveLocalJsonData(JsonSerializer.Serialize(Levels.EntriesToModels(LevelItems)));
            }
        }
        else
        {
            if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.L))
            {
                Levels.LoadLocalBinaryData(s =>
                {
                    AddMenuItems.Clear();
                    UI.PlaceableComponents.Clear();

                    foreach (var b in Intrinsics)
                        AddMenu(new ComponentEntry(b));

                    foreach (var entry in Levels.LoadFromUrlSafeBinary(s, AddMenuItems))
                    {
                        AddMenu(entry);
                        SetLevel(entry);
                    }
                });
            }

            if (InputHelper.Down(Keys.LeftAlt) && InputHelper.RisingEdge(Keys.S))
            {
                Levels.SaveLocalBinaryData(Levels.SaveToUrlSafeBinary(LevelItems));
            }
        }
    }

    protected override IEnumerable LevelLogic()
    {
        foreach (var b in Intrinsics)
            AddMenu(new ComponentEntry(b));

        SetLevel(new ComponentEntry(new Blueprint(new Simulation(128, 128), "Sandbox", [(default, TileKind.Component)]), new TestCases([])));

        UI.NewComponentButton.Visible = true;

        while (true)
        {
            Update();
            yield return null;
        }
    }
}
