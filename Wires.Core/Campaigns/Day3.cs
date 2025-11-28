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

internal class Day4 : Campaign
{
    public Day4(ServiceContainer serviceContainer, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction)
        : base("Day 4", serviceContainer, graphics, editorUI, simInteraction)
    {
    }

    protected override IEnumerable LevelLogic()
    {
        NextButtonClicked += () => NextButtonVisible = false;

        AddMenu(new ComponentEntry(Blueprint.NOT));
        AddMenu(new ComponentEntry(Blueprint.AND));
        AddMenu(new ComponentEntry(Blueprint.OR));
        AddMenu(new ComponentEntry(Blueprint.XOR));
        AddMenu(new ComponentEntry(Blueprint.Switch));

        CreateNewAdderLevel(
            1,
            "Half Adder",
            new TestCases([
                ([PowerState.OffState, PowerState.OffState], [PowerState.OffState, PowerState.OffState]),
                ([PowerState.OffState, PowerState.OnState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OnState, PowerState.OffState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OnState, PowerState.OnState], [PowerState.OffState, PowerState.OnState]),
            ]),
            TruthTableData.HALF_ADDER,
            10, 10
        );

        while (!_passAllCases)
            yield return null;
        UI.NextButton.Visible = true;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewAdderLevel(
            2,
            "Full Adder",
            new TestCases([
                ([PowerState.OffState, PowerState.OffState, PowerState.OffState], [PowerState.OffState, PowerState.OffState]),
                ([PowerState.OffState, PowerState.OffState, PowerState.OnState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OffState, PowerState.OnState, PowerState.OffState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OffState, PowerState.OnState, PowerState.OnState], [PowerState.OffState, PowerState.OnState]),
                ([PowerState.OnState, PowerState.OffState, PowerState.OffState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OnState, PowerState.OffState, PowerState.OnState], [PowerState.OffState, PowerState.OnState]),
                ([PowerState.OnState, PowerState.OnState, PowerState.OffState], [PowerState.OffState, PowerState.OnState]),
                ([PowerState.OnState, PowerState.OnState, PowerState.OnState], [PowerState.OnState, PowerState.OnState]),
            ]),
            TruthTableData.FULL_ADDER,
            14, 14
        );

        while (!_passAllCases)
            yield return null;

        yield break;

        void CreateNewAdderLevel(int levelIndex, string name, TestCases tests, TruthTableData truthTable, int width, int height)
        {
            int inputCount = tests.Enumerable!.First().Item1.Length;
            int outputCount = tests.Enumerable!.First().Item2.Length;

            var level = new Simulation(width, height);

            for (int i = 0; i < inputCount; i++)
                level.Place(Blueprint.Input, new(0, 2 + i * 3), 0, false, i);

            for (int i = 0; i < outputCount; i++)
                level.Place(Blueprint.Output, new(width - 1, i * 3), 0, false, i);

            _timeSinceLastTestCase = 0;

            SetLevel(new ComponentEntry(
                new Blueprint(level, name, [(default, TileKind.Component)]),
                tests,
                truthTable
            ));
        }
    }
}