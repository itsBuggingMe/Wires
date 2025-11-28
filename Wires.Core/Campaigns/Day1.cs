using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Paper.Core;
using Paper.Core.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Wires.Core.Sim;
using Wires.Core.Sim.Saving;
using Wires.Core.UI;

namespace Wires.Core.Campaigns;

internal class Day1 : Campaign
{
    public Day1(ServiceContainer serviceContainer, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction) : base("Day 1", serviceContainer, graphics, editorUI, simInteraction)
    {

    }

    protected override IEnumerable LevelLogic()
    {
        // this sucks
        NextButtonClicked += () => NextButtonVisible = false;

        var level1 = new Simulation(9, 9);
        level1.Place(Blueprint.Output, new(8, 4), 0, false);
        level1.Place(Blueprint.Switch, new(0, 4), 0, false);

        SetLevel(new ComponentEntry(new Blueprint(level1, "Wires", [(default, TileKind.Component)])));

        while (LevelItems[0].Blueprint.OutputBuffer(0).Off)
            yield return null;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        var level2 = new Simulation(9, 9);

        level2.Place(Blueprint.Output, new(8, 4), 0, false);
        int switchInput2 = level2.Place(Blueprint.Switch, new(0, 4), 0, false);
        level2.Place(Blueprint.NOT, new(4, 4), 0, false);

        Blueprint level2Blueprint;

        SetLevel(new ComponentEntry(level2Blueprint = new Blueprint(level2, "NOT", [(default, TileKind.Component)]), truthTable: TruthTableData.NOT));

        HashSet<PowerState> seenStates = [];

        while (seenStates.Count < 2)
        {
            ref Component @switch = ref level2.GetComponent(switchInput2);
            PowerState oldPowerState = @switch.Blueprint.SwitchValue;

            PowerState TestOutputPower(PowerState input, Blueprint b)
            {
                b.SwitchValue = input;
                level2Blueprint.SimulateTick(StateTable);
                return level2Blueprint.OutputBuffer(0);
            }

            if (TestOutputPower(PowerState.OnState, @switch.Blueprint).Off && TestOutputPower(PowerState.OffState, @switch.Blueprint).On)
            {
                seenStates.Add(oldPowerState);
            }

            @switch.Blueprint.SwitchValue = oldPowerState;
            level2Blueprint.SimulateTick(StateTable);
            yield return null;
        }

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        var level3 = new Simulation(9, 9);

        level3.Place(Blueprint.Output, new(8, 4), 0, false);
        int switchInput3_1 = level3.Place(Blueprint.Switch, new(0, 2), 0, false);
        int switchInput3_2 = level3.Place(Blueprint.Switch, new(0, 6), 0, false);
        level3.Place(Blueprint.AND, new(4, 4), 0, false);

        Blueprint level3Blueprint;
        SetLevel(new ComponentEntry(level3Blueprint = new Blueprint(level3, "AND", [(default, TileKind.Component)]), truthTable: TruthTableData.AND));

        HashSet<(PowerState, PowerState)> seenStates3 = [];

        while (seenStates3.Count < 4)
        {
            if (TestSim(
                [
                    [(switchInput3_1, PowerState.OffState), (switchInput3_2, PowerState.OffState)],
                    [(switchInput3_1, PowerState.OffState), (switchInput3_2, PowerState.OnState)],
                    [(switchInput3_1, PowerState.OnState), (switchInput3_2, PowerState.OffState)],
                    [(switchInput3_1, PowerState.OnState), (switchInput3_2, PowerState.OnState)],
                ],
                [PowerState.OffState, PowerState.OffState, PowerState.OffState, PowerState.OnState], level3Blueprint))
            {
                seenStates3.Add((level3.GetComponent(switchInput3_1).Blueprint.SwitchValue,
                    level3.GetComponent(switchInput3_2).Blueprint.SwitchValue));
            }
            yield return null;
        }

        bool TestSim(Span<(int ComponentId, PowerState State)[]> inputs, PowerState[] expected, Blueprint blueprint)
        {
            if (blueprint.Custom is null)
                return false;

            Span<PowerState> oldStates = stackalloc PowerState[inputs.Length];
            int index = 0;
            foreach (var componentId in inputs[0])
                oldStates[index++] = blueprint.Custom.GetComponent(componentId.ComponentId).Blueprint.SwitchValue;

            index = 0;
            bool result = true;
            foreach (var t in inputs)
            {
                foreach (var (id, state) in t)
                {
                    blueprint.Custom.GetComponent(id).Blueprint.SwitchValue = state;
                }

                blueprint.SimulateTick(StateTable);

                if (expected[index++] != blueprint.OutputBuffer(0))
                {
                    result = false;
                    break;
                }
            }

            index = 0;
            foreach (var componentId in inputs[0])
                blueprint.Custom.GetComponent(componentId.ComponentId).Blueprint.SwitchValue = oldStates[index++];

            blueprint.SimulateTick(StateTable);

            return result;
        }

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        UI.AddButton.Visible = true;

        AddMenu(new ComponentEntry(Blueprint.NOT));
        AddMenu(new ComponentEntry(Blueprint.AND));

        CreateNewLogicLevel(3, "NAND", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OffState]),
            ]), TruthTableData.NAND);

        while (!_passAllCases)
            yield return null;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewLogicLevel(4, "OR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OffState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OnState]),
            ]), TruthTableData.OR);

        while (!_passAllCases)
            yield return null;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewLogicLevel(5, "NOR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OffState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OffState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OffState]),
            ]), TruthTableData.NOR);

        while (!_passAllCases)
            yield return null;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        var sandbox = new Simulation(9, 9);

        sandbox.Place(Blueprint.Output, new(8, 4), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 2), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 6), 0, false);

        AddMenu(new ComponentEntry(Blueprint.NAND));
        AddMenu(new ComponentEntry(Blueprint.OR));
        AddMenu(new ComponentEntry(Blueprint.NOR));

        SetLevel(new ComponentEntry(new Blueprint(sandbox, "Sandbox", [(default, TileKind.Component)]), null));

        void CreateNewLogicLevel(int levelIndex, string name, TestCases? tests, TruthTableData? truthTable)
        {
            var leveln = new Simulation(9, 9);

            leveln.Place(Blueprint.Output, new(8, 4), 0, false);
            leveln.Place(Blueprint.Input, new(0, 2), 0, false);
            leveln.Place(Blueprint.Input, new(0, 6), 0, false, inputOutputId: 1);
            _timeSinceLastTestCase = 0;

            SetLevel(new ComponentEntry(new Blueprint(leveln, name, [(default, TileKind.Component)]), tests, truthTable));
        }
    }
}
