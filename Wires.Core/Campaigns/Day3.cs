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

internal class Day3 : Campaign
{
    public Day3(ServiceContainer serviceContainer, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction)
        : base("Day 3", serviceContainer, graphics, editorUI, simInteraction)
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

        CreateNewDecoderLevel(
            1,
            "1-Bit Decoder",
            new TestCases([
                ([PowerState.OffState], [PowerState.OnState, PowerState.OffState]),
                ([PowerState.OnState], [PowerState.OffState, PowerState.OnState]),
            ]),
            9, 8
        );

        while (!_passAllCases)
            yield return null;
        UI.NextButton.Visible = true;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewDecoderLevel(
            2,
            "2-Bit Decoder",
            new TestCases(GenerateDecoderCases(2)),
            12, 14
        );

        while (!_passAllCases)
            yield return null;
        UI.NextButton.Visible = true;

        NextButtonVisible = true;
        while (NextButtonVisible)
            yield return null;

        CreateNewDecoderLevel(
            3,
            "3-Bit Decoder",
            new TestCases(GenerateDecoderCases(3)),
            16, 24
        );

        while (!_passAllCases)
            yield return null;

        yield break;

        static (PowerState[], PowerState[])[] GenerateDecoderCases(int bits)
        {
            int outputs = 1 << bits;
            var cases = new List<(PowerState[], PowerState[])>();

            for (int i = 0; i < outputs; i++)
            {
                var inputStates = new PowerState[bits];
                var outputStates = new PowerState[outputs];

                for (int b = 0; b < bits; b++)
                {
                    int shift = bits - 1 - b;
                    inputStates[^(b + 1)] = ((i >> shift) & 1) == 1
                        ? PowerState.OnState
                        : PowerState.OffState;
                }

                for (int j = 0; j < outputs; j++)
                    outputStates[j] = (j == i) ? PowerState.OnState : PowerState.OffState;

                cases.Add((inputStates, outputStates));
            }

            return cases.ToArray();
        }

        void CreateNewDecoderLevel(int levelIndex, string name, TestCases tests, int width, int height)
        {
            int inputCount = tests.Enumerable!.First().Item1.Length;
            int outputCount = tests.Enumerable!.First().Item2.Length;

            var level = new Simulation(width, height);

            // Inputs (left side)
            for (int i = 0; i < inputCount; i++)
                level.Place(Blueprint.Input, new(0, 2 + i * 3), 0, false, i);

            // Outputs (right side)
            for (int i = 0; i < outputCount; i++)
                level.Place(Blueprint.Output, new(width - 1, i * 3), 0, false, i);

            _timeSinceLastTestCase = 0;

            SetLevel(new ComponentEntry(
                new Blueprint(level, name, [(default, TileKind.Component)]),
                tests,
                null
            ));
        }
    }
}
