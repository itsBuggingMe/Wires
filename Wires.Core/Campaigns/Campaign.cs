using Paper.Core;
using Paper.Core.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Wires.Core.Sim;
using Wires.Core.UI;
using System.Collections.ObjectModel;

namespace Wires.Core;

internal abstract class Campaign
{
    public List<ComponentEntry> AddMenuItems = [];
    public List<ComponentEntry> LevelItems = [];

    public void AddMenu(ComponentEntry componentEntry)
    {
        AddMenuItems.Add(componentEntry);
        UI.PlaceableComponents.Add(componentEntry);
    }

    public string Name { get; private set; }

    protected readonly EditorUI UI;
    protected TruthTable? _currentDisplayTable;

    protected SimInteraction _interaction;

    public SimInteraction Interaction => _interaction;

    private ComponentEntry? CurrentEntry => UI.CurrentEntry;

    protected readonly Graphics _graphics;
    
    protected readonly IEnumerator _levelStateMachine;
    protected int _testCaseIndex;
    protected bool _passAllCases;

    protected PowerState[] _outputTempBuffer = new PowerState[128];

    protected const int Padding = Constants.Padding;

    public Campaign(string name, ServiceContainer serviceContainer, Graphics graphics, EditorUI editorUI, SimInteraction simInteraction)
    {
        Name = name;
        _graphics = graphics;
        _interaction = simInteraction;

        UI = editorUI;

        // levels
        _levelStateMachine = LevelLogic().GetEnumerator();
    }

    protected int _timeSinceLastTestCase = 0;
    public void Update(Time gameTime)
    {
        _timeSinceLastTestCase++;

        if (!UI.Update())
        {
            _interaction.UpdateSim();
            _interaction.UpdateCamera();

            if (InputHelper.FallingEdge(MouseButton.Left))
            {
                if (!UI.Menu.Bounds.Contains(InputHelper.MouseLocation))
                    UI.Menu.Visible = false;
                if (!UI.AddMenu.Bounds.Contains(InputHelper.MouseLocation))
                    UI.AddMenu.Visible = false;
            }
        }

        _levelStateMachine.MoveNext();

        if (CurrentEntry?.TestCases is not null && _timeSinceLastTestCase > 30 && UI.IsPlaying)
        {
            _timeSinceLastTestCase = 0;
            _testCaseIndex = (_testCaseIndex + 1) % CurrentEntry.TestCases.Length;
            CurrentEntry.TestCases.Set(_testCaseIndex, CurrentEntry.Blueprint.InputBufferRaw, _outputTempBuffer);

            CurrentEntry.Blueprint.StepStateful();

            if (!CurrentEntry.Blueprint.OutputBufferRaw.AsSpan().SequenceEqual(_outputTempBuffer.AsSpan(0, CurrentEntry.Blueprint.OutputBufferRaw.Length)))
            {
                UI.IsPlaying = false;
            }
            else if (_testCaseIndex == CurrentEntry.TestCases.Length - 1)
            {
                _passAllCases = true;
            }
        }
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);
        _graphics.StartBatches(true);
        CurrentEntry?.Custom?.Draw(_graphics);
        _interaction.Draw();
        _graphics.EndBatches();
        _graphics.StartBatches();
        UI.DoDraw();
        _graphics.EndBatches();
    }

    protected abstract IEnumerable LevelLogic();

    protected void SetLevel(ComponentEntry entry)
    {
        if(!UI.Levels.Contains(entry))
        {
            UI.Levels.Add(entry);
            LevelItems.Add(entry);
        }
        UI.SwitchLevel(entry);
    }

    
}
