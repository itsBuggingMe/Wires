using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Wires.Core;
using Wires.Sim.Saving;
using Wires.Sim;
using System.Text.Json;
using MonoGameGum;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using MonoGameGum.Input;

namespace Wires.States;

public partial class MainSimulation : IScreen
{
    private readonly Camera2D _camera;
    private readonly ShapeBatch _sb;
    private readonly Graphics _graphics;
    private readonly ScreenManager _screenManager;

    private Point? _dragStartWorld;

    private ShortCircuitDescription? _shortCircuitErr;

    private enum SimDragReason : byte { PlaceWire, MoveComponent }
    private SimDragReason _simDragReason;
    private int _draggedComponentId;

    private enum PlayButtonState : byte { Play, Pause }
    private PlayButtonState _playState;
    private int CurrentTestCaseIndex
    { 
        get => _currentEntry?.TestCaseIndex ?? 0;
        set
        {
            if(_currentEntry is { } entry)
                entry.TestCaseIndex = value;
        }
    }
    private float _testCaseTimer;

    internal IReadOnlyList<ComponentEntry> Components => _components;

    private readonly List<ComponentEntry> _components = [];
    private ComponentEntry? _currentEntry;

    private PowerState[] _outputTempBuffer = new PowerState[128];

    private int _rotation;

    private IEnumerator<ShortCircuitDescription?>? _state;

    public MainSimulation(Camera2D camera, Graphics graphics, ScreenManager screenManager) : this(graphics, camera, screenManager)
    {
        AddComponent(new(Blueprint.NAND));

        foreach (var c in Levels.LoadLevels(_components))
            AddComponent(c);
    }

    public void Update(Time gameTime)
    {
        //_camera.Scale = Vector2.One * 1 / 0.34609375f;
        _testCaseTimer += gameTime.FrameDeltaTime;

        if(_testCaseTimer > 10 || InputHelper.Down(Keys.LeftShift))
        {
            _testCaseTimer = 0;
            if(_playState == PlayButtonState.Pause && _currentEntry is not null)
            {
                if (_currentEntry.TestCases is null || _currentEntry.TestCases.Length == 0)
                {
                    _currentEntry?.Blueprint?.StepStateful();
                    //if (_state is not null && !_state.MoveNext())
                    //    CurrentEntry.Custom?.RecordDelayValues();
                }
                else
                {
                    CurrentTestCaseIndex++;
                    if (CurrentTestCaseIndex >= _currentEntry.TestCases.Length)
                    {
                        _playState = PlayButtonState.Play;
                    }
                    else
                        TestTestCase();
                }
            }
        }

        UpdateInputs();
    }

    private void UpdateInputs()
    {
        //if(InputHelper.RisingEdge(Keys.J))
        //{
        //    _state = CurrentEntry?.Custom?.StepEnumerator(CurrentEntry.Blueprint).GetEnumerator();
        //}

        if (UpdateUi() || GumService.Default.Cursor.WindowOver != null)
            return;

        if (_currentEntry is { Custom: not null } x && SimInteract(x.Blueprint))
            return;

        UpdateCamera();
    }


    private void ResetSimulation()
    {
        CurrentTestCaseIndex = 0;
        _shortCircuitErr = null;
        _currentEntry?.Custom?.ClearAllDelayValues();
        _currentEntry?.Blueprint.Reset();
    }

    private void TestTestCase()
    {
        if(_currentEntry is { TestCases: { } cases, Blueprint: { } blueprint })
        {
            cases.Set(CurrentTestCaseIndex, blueprint.InputBufferRaw, _outputTempBuffer);
            blueprint.StepStateful();

            if (!blueprint.OutputBufferRaw.AsSpan().SequenceEqual(_outputTempBuffer.AsSpan(0, blueprint.OutputBufferRaw.Length)))
            {
                _playState = PlayButtonState.Play;
            }
        }
    }

    private bool SimInteract(Blueprint blueprint)
    {
        var sim = blueprint.Custom ?? throw new ArgumentNullException();

        Point tileOver = GetTileOver();

        if (!sim.InRange(tileOver))
            return false;

        bool input = false;

        if (_dragStartWorld is not null)
            input = true;

        if (MouseButton.Left.RisingEdge() && sim.InRange(tileOver) && 
            (sim[tileOver].Kind is TileKind.Output or TileKind.Input or TileKind.Component || sim.IdOfWireAt(tileOver) is int))
        {
            input = true;
            _dragStartWorld = tileOver;
            _simDragReason = sim[tileOver].Kind == TileKind.Component ? SimDragReason.MoveComponent : SimDragReason.PlaceWire;
            _draggedComponentId = sim[tileOver].ComponentId;
        }

        if (MouseButton.Left.FallingEdge()
            && _dragStartWorld is Point dragStart)
        {
            if (sim.InRange(tileOver)
                && tileOver != dragStart
                && _simDragReason == SimDragReason.PlaceWire && sim[tileOver] is not { Kind: TileKind.Component })
            {
                sim.CreateWire(new Wire(dragStart, tileOver));
                ResetSimulation();
            }
            else
            {
                foreach (var component in sim.Components)
                {
                    if(component.Position == tileOver && component is { Blueprint.Descriptor: Blueprint.IntrinsicBlueprint.Switch })
                    {
                        component.Blueprint.SwitchValue = component.Blueprint.SwitchValue.On ? PowerState.OffState : PowerState.OnState;
                    }
                }
                ResetSimulation();
            }
            input = true;
            _dragStartWorld = null;
        }

        if(MouseButton.Left.Down() && _dragStartWorld is Point && _simDragReason == SimDragReason.MoveComponent)
        {
            sim.MoveComponent(_draggedComponentId, tileOver);
            ResetSimulation();
        }

        if (MouseButton.Right.FallingEdge())
        {
            input = true;
            if(sim.IdOfWireAt(tileOver) is int id)
            {
                sim.DestroyWire(id);
            }
            else if (sim[tileOver].Kind is not TileKind.Nothing)
            {
                sim.DestroyComponent(tileOver);
            }
            ResetSimulation();
        }

        return input;
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);

        _sb.Begin(_camera.View, _camera.Projection);
        _graphics.SpriteBatch.Begin(transformMatrix: _camera.View);
        _graphics.SpriteBatchText.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.View);

        if (_currentEntry is { } entry)
        {
            entry.Custom?.Draw(_graphics, Step);

            if (_componentToPlace is not null && GumService.Default.Cursor.WindowOver is null)
            {
                _componentToPlace.Blueprint.Draw(_graphics, null, GetTileOver(), Step, Constants.WireRad, false, 0, rotationOverride: _rotation);
            }

            var over = GetTileOver();
            if (_dragStartWorld is Point dragStart
                && (entry.Custom?.InRange(over) ?? false)
                && over != dragStart
                && _simDragReason == SimDragReason.PlaceWire)
            {
                var colors = Constants.GetWireColor(PowerState.OffState);
                _currentEntry?.Custom?.DrawWire(_sb, Step, new Wire(dragStart, over), colors.Color * 0.5f, colors.Output * 0.5f);
            }
        }

        _sb.End();
        _graphics.SpriteBatch.End();
        _graphics.SpriteBatchText.End();

        // ui
        DrawUi();
    }

    const int Padding = 10;
    const int Rounding = 10;

    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-Step / 2)) / new Vector2(Step)).ToPoint();

    private void UpdateCamera()
    {
        const float ScrollScaleM = 1.2f;
        if (InputHelper.DeltaScroll != 0)
            _camera.Scale *= InputHelper.DeltaScroll > 0 ? ScrollScaleM : 1 / ScrollScaleM;
        if (Keys.OemPlus.RisingEdge())
            _camera.Scale *= ScrollScaleM;
        if (Keys.OemMinus.RisingEdge())
            _camera.Scale /= ScrollScaleM;

        if (MouseButton.Left.Down() && _dragStartWorld is null)
            _camera.Position -= (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2()) / _camera.Scale;
    }

    const float Step = 50f;


    public void OnEnter(IScreen previous, object? args)
    {
        if(args is ComponentEditorResult model)
        {
            InitUi(_screenManager, _graphics);

            if(model.ResultModel is not null)
            {
                foreach (var level in Levels.CreateComponentEntriesFromModels(_components, [model.ResultModel]))
                    _components.Add(level);
            }

            foreach (var entry in _components)
                AddComponent(entry);
        }
    }

    public object? OnExit()
    {
        GumService.Default.Root.Children.Clear();
        return null;
    }
}
