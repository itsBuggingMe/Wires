using Paper.Core;
using Paper.Core.UI;
using Wires.Core.UI;
using Microsoft.Xna.Framework;
using System;
using Wires.Core.States;
using Wires.Core.Sim;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Campaign1.HotReloadManager))]
namespace Wires.Core.States;

internal class Campaign1 : IScreen
{
    private RootUI<Graphics> _root;
    private Button _playButton = null!;
    private Button _nextButton = null!;
    private StackPanel _menu = null!;
    private StackPanel _addMenu = null!;
    private Button _addButton = null!;
    private TruthTable? _currentDisplayTable;

    private readonly List<ComponentEntry> _levels = [];
    private ComponentEntry _currentEntry = null!;
    private SimInteraction _interaction;

    private readonly Graphics _graphics;
    private bool _isPlaying;

    private readonly IEnumerator _levelStateMachine;
    private int _testCaseIndex;
    private bool _passAllCases;

    private PowerState[] _outputTempBuffer = new PowerState[128];

    public Campaign1(Graphics graphics)
    {
        _graphics = graphics;
        _interaction = new SimInteraction(graphics);
        const int Padding = Constants.Padding;

        _root = CreateRoot();
        //HotReloadManager.HotReloadOccured += _ => _root = CreateRoot();

        RootUI<Graphics> CreateRoot() => new(graphics.Game, graphics, 1920, 1080)
        {
            Children =
            [
                // pancake menu
                new Button(new(Padding), new(48, false), Button.Pancake)
                {
                    Children = [_menu = new StackPanel(new(48, Padding), new UIVector2(40), false)
                    {
                        Children = GetMenuElements(),
                        Visible = false,
                    }],
                    Clicked = p => {
                        _menu.Visible = !_menu.Visible;
                    }
                },
                _addButton = new Button(new(Padding, 64 + Padding, false, false), new(48, false), Button.Plus)
                {
                    Visible = false,
                    Children = [_addMenu = new StackPanel(new(48, 0), new UIVector2(40))
                    {
                        Children = [
                            new Button(default, new Vector2(80, 36), Button.None)
                            {
                                Text = new Text(new(40, 18), "NOT") { ElementAlign = ElementAlign.Center },
                                RisingEdge = p => _interaction.BeginPlaceComponent(new ComponentEntry(Blueprint.NOT)),
                            },
                            new Button(default, new Vector2(80, 36), Button.None)
                            {
                                Text = new Text(new(40, 18), "AND") { ElementAlign = ElementAlign.Center },
                                RisingEdge = p => _interaction.BeginPlaceComponent(new ComponentEntry(Blueprint.AND)),
                            },
                        ],
                        Visible = false,
                        ColorMultipler = 0,
                    }],
                    Clicked = p => {
                        _addMenu.Visible = !_addMenu.Visible;
                    }
                },
                _playButton = new Button(new(1920 - Padding, Padding), new(64), b => {
                    var sb = b.Graphics.ShapeBatch;
                    var p = b.Bounds;

                    if(!_isPlaying)
                    {
                        sb.DrawEquilateralTriangle(p.Center.ToVector2() - Vector2.UnitX * 3, 12, Color.Green * b.TactileFeedback, Color.DarkGreen * b.TactileFeedback, 3, 4, -MathHelper.PiOver2);
                    }
                    else
                    {
                        var size = new Vector2(12, 35);
                        sb.DrawRectangle(p.Center.ToVector2() - size * 0.5f + Vector2.UnitX * -10f, size, Color.Green * b.TactileFeedback, Color.DarkGreen * b.TactileFeedback, 2, 4);
                        sb.DrawRectangle(p.Center.ToVector2() - size * 0.5f + Vector2.UnitX * 10f, size, Color.Green * b.TactileFeedback, Color.DarkGreen * b.TactileFeedback, 2, 4);
                    }
                })
                {
                    ElementAlign = ElementAlign.TopRight,
                    Clicked = p => { _isPlaying = !_isPlaying; _testCaseIndex = 0; },
                    Children = [
                        new Text(new(-64 - Padding, Padding, false, false), contentSource: () => _currentEntry.TestCases is null ? string.Empty : $"Test {_testCaseIndex + 1}/{_currentEntry.TestCases.Length}")
                        {
                            ElementAlign = ElementAlign.TopRight,
                        }
                    ]
                },
                _nextButton = new Button(new(1920 - Padding, 1080 - Padding), new(280, 60), Button.None)
                {
                    ElementAlign = ElementAlign.BottomRight,
                    Text = new Text(new(-140, -30), "Next Level") { ElementAlign = ElementAlign.Center },
                    Visible = false,
                },
                new Text(new(1920 / 2, Padding), contentSource: () => _currentEntry?.Name)
                {
                    ElementAlign = ElementAlign.TopMiddle,
                    Scale = Vector2.One * 2,
                },
            ],
        };

        // levels
        _levelStateMachine = LevelLogic().GetEnumerator();
        _levelStateMachine.MoveNext();
    }

    private int _timeSinceLastTestCase = 0;
    public void Update(Time gameTime)
    {
        _timeSinceLastTestCase++;

        if (InputHelper.FallingEdge(MouseButton.Left))
        {
            if (!_menu.Bounds.Contains(InputHelper.MouseLocation))
                _menu.Visible = false;
            if (!_addMenu.Bounds.Contains(InputHelper.MouseLocation))
                _addMenu.Visible = false;
        }

        if (!_root.Update())
        {
            _interaction.UpdateSim();
            _interaction.UpdateCamera();
        }
        

        _levelStateMachine.MoveNext();

        if (_currentEntry.TestCases is not null && _timeSinceLastTestCase > 30 && _isPlaying)
        {
            _timeSinceLastTestCase = 0;
            _testCaseIndex = (_testCaseIndex + 1) % _currentEntry.TestCases.Length;
            _currentEntry.TestCases.Set(_testCaseIndex, _currentEntry.Blueprint.InputBufferRaw, _outputTempBuffer);

            _currentEntry.Blueprint.StepStateful();

            if (!_currentEntry.Blueprint.OutputBufferRaw.AsSpan().SequenceEqual(_outputTempBuffer.AsSpan(0, _currentEntry.Blueprint.OutputBufferRaw.Length)))
            {
                _isPlaying = false;
            }
            else if (_testCaseIndex == _currentEntry.TestCases.Length - 1)
            {
                _passAllCases = true;
            }
        }
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);
        _graphics.StartBatches(true);
        _currentEntry.Custom?.Draw(_graphics);
        _interaction.Draw();
        _graphics.EndBatches();
        _graphics.StartBatches();
        _root.DoDraw();
        _graphics.EndBatches();
    }

    public void OnEnter(IScreen previous, object? args) { }
    public object? OnExit() => null;

    private IEnumerable LevelLogic()
    {
        bool nextButtonClicked = false;
        _nextButton.Clicked = _ => nextButtonClicked = true;

        var level1 = new Simulation(9, 9);
        level1.Place(Blueprint.Output, new(8, 4), 0, false);
        level1.Place(Blueprint.Switch, new(0, 4), 0, false);

        SwitchLevel(_currentEntry = new ComponentEntry(new Blueprint(level1, "Wires", [(default, TileKind.Component)])));

        while (_levels[0].Blueprint.OutputBuffer(0).Off)
            yield return null;

        _nextButton.Visible = true;
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        var level2 = new Simulation(9, 9);

        level2.Place(Blueprint.Output, new(8, 4), 0, false);
        int switchInput2 = level2.Place(Blueprint.Switch, new(0, 4), 0, false);
        level2.Place(Blueprint.NOT, new(4, 4), 0, false);

        Blueprint level2Blueprint;

        SwitchLevel(_currentEntry = new ComponentEntry(level2Blueprint = new Blueprint(level2, "NOT", [(default, TileKind.Component)]), truthTable: TruthTableData.NOT));

        HashSet<PowerState> seenStates = [];

        while(seenStates.Count < 2)
        {
            ref Component @switch = ref level2.GetComponent(switchInput2);
            PowerState oldPowerState = @switch.Blueprint.SwitchValue;
            
            PowerState TestOutputPower(PowerState input, Blueprint b)
            {
                b.SwitchValue = input;
                level2Blueprint.StepStateful();
                return level2Blueprint.OutputBuffer(0);
            }

            if(TestOutputPower(PowerState.OnState, @switch.Blueprint).Off && TestOutputPower(PowerState.OffState, @switch.Blueprint).On)
            {
                seenStates.Add(oldPowerState);
            }

            @switch.Blueprint.SwitchValue = oldPowerState;
            level2Blueprint.StepStateful();
            yield return null;
        }

        _nextButton.Visible = true;
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        var level3 = new Simulation(9, 9);

        level3.Place(Blueprint.Output, new(8, 4), 0, false);
        int switchInput3_1 = level3.Place(Blueprint.Switch, new(0, 2), 0, false);
        int switchInput3_2 = level3.Place(Blueprint.Switch, new(0, 6), 0, false);
        level3.Place(Blueprint.AND, new(4, 4), 0, false);

        Blueprint level3Blueprint;
        SwitchLevel(_currentEntry = new ComponentEntry(level3Blueprint = new Blueprint(level3, "AND", [(default, TileKind.Component)]), truthTable: TruthTableData.AND));

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
            foreach(var t in inputs)
            {
                foreach(var (id, state) in t)
                {
                    blueprint.Custom.GetComponent(id).Blueprint.SwitchValue = state;
                }

                blueprint.StepStateful();

                if (expected[index++] != blueprint.OutputBuffer(0))
                {
                    result = false;
                    break;
                }
            }

            index = 0;
            foreach (var componentId in inputs[0])
                blueprint.Custom.GetComponent(componentId.ComponentId).Blueprint.SwitchValue = oldStates[index++];

            blueprint.StepStateful();

            return result;
        }

        _nextButton.Visible = true;
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        _addButton.Visible = true;

        CreateNewLogicLevel(3, "NAND", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OffState]),
            ]), TruthTableData.NAND);

        while (!_passAllCases)
            yield return null;
        _nextButton.Visible = true;
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        CreateNewLogicLevel(4, "OR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OffState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OnState]),
            ]), TruthTableData.OR);

        while (!_passAllCases)
            yield return null;
        _nextButton.Visible = true;
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        CreateNewLogicLevel(5, "NOR", new TestCases([
            ([PowerState.OffState, PowerState.OffState], [PowerState.OnState]),
            ([PowerState.OffState, PowerState.OnState], [PowerState.OffState]),
            ([PowerState.OnState, PowerState.OffState], [PowerState.OffState]),
            ([PowerState.OnState, PowerState.OnState], [PowerState.OffState]),
            ]), TruthTableData.NOR);

        while (!_passAllCases)
            yield return null;
        _nextButton.Visible = true;
        _nextButton.Text!.Content = "Sandbox";
        while (!nextButtonClicked)
            yield return null;
        _nextButton.Visible = nextButtonClicked = false;

        var sandbox = new Simulation(9, 9);

        sandbox.Place(Blueprint.Output, new(8, 4), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 2), 0, false);
        sandbox.Place(Blueprint.Switch, new(0, 6), 0, false);

        Button ButtonOf(string name, Blueprint blueprint) => new Button(default, new Vector2(80, 36), Button.None)
        {
            Text = new Text(new(40, 18), name) { ElementAlign = ElementAlign.Center },
            RisingEdge = p => _interaction.BeginPlaceComponent(new ComponentEntry(blueprint)),
        };

        _addMenu.AddChild(ButtonOf("NAND", Blueprint.NAND));
        _addMenu.AddChild(ButtonOf("OR", Blueprint.OR));
        _addMenu.AddChild(ButtonOf("NOR", Blueprint.NOR));

        SwitchLevel(_currentEntry = new ComponentEntry(new Blueprint(sandbox, "Sandbox", [(default, TileKind.Component)]), null));

        void CreateNewLogicLevel(int levelIndex, string name, TestCases? tests, TruthTableData? truthTable)
        {
            var leveln = new Simulation(9, 9);

            leveln.Place(Blueprint.Output, new(8, 4), 0, false);
            leveln.Place(Blueprint.Input, new(0, 2), 0, false);
            leveln.Place(Blueprint.Input, new(0, 6), 0, false, inputOutputId: 1);
            _timeSinceLastTestCase = 0;

            SwitchLevel(_currentEntry = new ComponentEntry(new Blueprint(leveln, name, [(default, TileKind.Component)]), tests, truthTable));
        }
    }

    private void SwitchLevel(ComponentEntry entry)
    {
        if(!_levels.Contains(entry))
        {
            _levels.Add(entry);
            _menu.Children = GetMenuElements();
        }

        _playButton.Visible = entry.TestCases is not null;
        _currentEntry = entry;
        if(entry.Custom is not null)
            _interaction.ActiveEntry = entry;
        _passAllCases = false;
        _testCaseIndex = 0;
        _isPlaying = false;

        _currentDisplayTable?.Remove();

        if (entry.TruthTable is not null)
        {
            _currentDisplayTable = new TruthTable(_graphics, new UIVector2(1920 - Constants.Padding, 96, true, false), entry.TruthTable.Headers, entry.TruthTable.Rows)
            { ElementAlign = ElementAlign.TopRight };
            _root.AddChild(_currentDisplayTable);
        }
    }

    private List<UIBase<Graphics>> GetMenuElements()
    {
        return _levels.Select(l => (UIBase<Graphics>)new Button(default, new Vector2(100, 40), g => { })
        {
            Text = new Text(new(50, 20), l.Name) { ElementAlign = ElementAlign.Center },
            Clicked = _ => SwitchLevel(l),
        }).Prepend(new Frame(default, new Vector2(100, 20)) { Children = [new Text(new Vector2(50, 12), "Levels") { ElementAlign = ElementAlign.Center }] }).ToList();
    }

    public static class HotReloadManager
    {
        public static Action<Type[]?>? HotReloadOccured;

        public static void ClearCache(Type[]? updatedTypes)
        {

        }

        public static void UpdateApplication(Type[]? updatedTypes)
        {
            HotReloadOccured?.Invoke(updatedTypes);
        }
    }
}
