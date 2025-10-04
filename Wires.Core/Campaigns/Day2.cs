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

internal class Day2 : Campaign
{
    public Day2(ServiceContainer serviceContainer, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction) : base("Day 2", serviceContainer, graphics, editorUI, simInteraction)
    {

    }

    protected override IEnumerable LevelLogic()
    {
        NextButtonClicked += () => NextButtonVisible = false;

        AddMenu(new ComponentEntry(Blueprint.NOT));
        AddMenu(new ComponentEntry(Blueprint.AND));
        AddMenu(new ComponentEntry(Blueprint.NAND));
        AddMenu(new ComponentEntry(Blueprint.OR));
        AddMenu(new ComponentEntry(Blueprint.NOR));
        AddMenu(new ComponentEntry(Blueprint.Switch));

        CreateNewLogicLevel(1, "XOR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OffState]),
        ([PowerState.OffState, PowerState.OnState], [PowerState.OnState]),
        ([PowerState.OnState, PowerState.OffState], [PowerState.OnState]),
        ([PowerState.OnState, PowerState.OnState], [PowerState.OffState]),
        ]), TruthTableData.XOR);

        while (!_passAllCases)
            yield return null;
        UI.NextButton.Visible = true;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewLogicLevel(2, "XNOR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OnState]),
        ([PowerState.OffState, PowerState.OnState], [PowerState.OffState]),
        ([PowerState.OnState, PowerState.OffState], [PowerState.OffState]),
        ([PowerState.OnState, PowerState.OnState], [PowerState.OnState]),
    ]), TruthTableData.XNOR);

        while (!_passAllCases)
            yield return null;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        AddMenu(new ComponentEntry(Blueprint.XOR));
        AddMenu(new ComponentEntry(Blueprint.XNOR));

        var level3 = new Simulation(15, 15);

        for (int i = 0; i < 8; i++)
            level3.Place(Blueprint.Input, new(0, i * 2), 0, false, i);

        level3.Place(Blueprint.Output, new(14, 4), 0, false);

        var challengeBlueprint = new Blueprint(level3, "Challenge", [(default, TileKind.Component)]);

        var challengeCases = new TestCases(
            GenerateChallengeCases(8)
        );

        SetLevel(new ComponentEntry(challengeBlueprint, challengeCases, null));

        while (!_passAllCases)
            yield return null;

        UI.NextButton.Text!.Content = "Sandbox";

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        var sandbox = new Simulation(9, 9);
        sandbox.Place(Blueprint.Output, new(8, 4), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 2), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 6), 0, false);

        SetLevel(new ComponentEntry(new Blueprint(sandbox, "Sandbox", [(default, TileKind.Component)]), null));

        static (PowerState[], PowerState[])[] GenerateChallengeCases(int inputs)
        {
            var cases = new List<(PowerState[], PowerState[])>();
            int total = 1 << inputs;

            for (int i = 0; i < total; i++)
            {
                PowerState[] inputStates = new PowerState[inputs];
                int onCount = 0;
                for (int b = 0; b < inputs; b++)
                {
                    if (((i >> b) & 1) == 1)
                    {
                        inputStates[b] = PowerState.OnState;
                        onCount++;
                    }
                    else
                    {
                        inputStates[b] = PowerState.OffState;
                    }
                }

                PowerState[] output = [onCount % 2 == 1 ? PowerState.OnState : PowerState.OffState];
                cases.Add((inputStates, output));
            }

            return cases.ToArray();
        }

        void CreateNewLogicLevel(int levelIndex, string name, TestCases? tests, TruthTableData? truthTable)
        {
            var leveln = new Simulation(12, 12);

            leveln.Place(Blueprint.Output, new(11, 6), 0, false);
            leveln.Place(Blueprint.Input, new(0, 3), 0, false);
            leveln.Place(Blueprint.Input, new(0, 8), 0, false, inputOutputId: 1);
            _timeSinceLastTestCase = 0;

            SetLevel(new ComponentEntry(new Blueprint(leveln, name, [(default, TileKind.Component)]), tests, truthTable));
        }
    }

}
