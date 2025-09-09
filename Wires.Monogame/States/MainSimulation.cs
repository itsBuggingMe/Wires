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

namespace Wires.States;

public class MainSimulation : IScreen
{
    private Camera2D _camera;
    private ShapeBatch _sb;
    private Graphics _graphics;

    private Point? _dragStartWorld;
    private Point? _dragStartUi;
    private DragReason _dragReason;
    private int _wireDiagnalValue;
    private int _selectedComponentToPlace = -1;

    private ShortCircuitDescription? _shortCircuitErr;

    private enum DragReason : byte { Right = 1, Bottom = 2, Component = 4, }
    private enum SimDragReason : byte { PlaceWire, MoveComponent }
    private SimDragReason _simDragReason;
    private int _draggedComponentId;

    private enum PlayButtonState : byte { Play, Pause }
    private PlayButtonState _playState;
    private int CurrentTestCaseIndex
    { 
        get => CurrentEntry?.TestCaseIndex ?? 0;
        set
        {
            if(CurrentEntry is { } entry)
                entry.TestCaseIndex = value;
        }
    }
    private float _testCaseTimer;

    private readonly List<ComponentEntry> _components = [];
    private int _selectedSim = -1;

    private PowerState[] _outputTempBuffer = new PowerState[128];

    private ComponentEntry? CurrentEntry => _selectedSim == -1 ? null : _components[_selectedSim];

    private int _rotation;

    public MainSimulation(Camera2D camera, Graphics graphics)
    {
        _graphics = graphics;
        _camera = camera;
        _sb = graphics.ShapeBatch;

        _components.Add(new(Blueprint.NAND));

        _components.AddRange(Levels.LoadLevels(_components));

        _windowSize = new(_graphics.GraphicsDevice.Viewport.Width * 0.1f, _graphics.GraphicsDevice.Viewport.Height - 2 * Padding);
    }

    public void Update(Time gameTime)
    {
        //_camera.Scale = Vector2.One * 1 / 0.34609375f;
        _testCaseTimer += gameTime.FrameDeltaTime;

        if(_testCaseTimer > 30)
        {
            _testCaseTimer -= 30;
            if(_playState == PlayButtonState.Pause && CurrentEntry is not null)
            {
                CurrentTestCaseIndex++;
                if (CurrentTestCaseIndex >= CurrentEntry.TestCases!.Length)
                {
                    _playState = PlayButtonState.Play;
                }
                else
                    TestTestCase();
            }
        }

        UpdateInputs();
    }

    private void UpdateInputs()
    {
        if (InputHelper.Down(Keys.LeftAlt))
        {
            if (InputHelper.RisingEdge(Keys.L))
            {
                Levels.LoadLocalData(s =>
                {
                    LevelModel[] models = JsonSerializer.Deserialize<LevelModel[]>(s) ?? [];
                    _components.Clear();
                    _components.Add(new(Blueprint.NAND));
                    _components.Add(new(Blueprint.Delay));
                    _components.Add(new(Blueprint.On));
                    _components.Add(new(Blueprint.Off));
                    _components.AddRange(Levels.CreateComponentEntriesFromModels(_components, models));
                    ResetSimulation();
                });
            }
            else if (InputHelper.RisingEdge(Keys.S))
            {
                Levels.SaveLocalData(Levels.SerializeComponentEntries(_components));
            }
        }

        if (UiInput())
            return;

        if (CurrentEntry is { Custom: not null } x && SimInteract(x.Blueprint))
            return;

        UpdateCamera();
    }

    private Rectangle Sidebar => new Rectangle(new(Padding), _windowSize.ToPoint());
    private Rectangle Play => new Rectangle(_graphics.GraphicsDevice.Viewport.Width - 64 - Padding, Padding, 64, 64);

    private void ResetSimulation()
    {
        CurrentTestCaseIndex = 0;
        _shortCircuitErr = null;
        CurrentEntry?.Custom?.ClearAllDelayValues();
        CurrentEntry?.Blueprint.Reset();
    }

    private void TestTestCase()
    {
        if(CurrentEntry is { TestCases: { } cases, Blueprint: { } blueprint })
        {
            cases.Set(CurrentTestCaseIndex, blueprint.InputBufferRaw, _outputTempBuffer);
            blueprint.StepStateful();

            if (!blueprint.OutputBufferRaw.AsSpan().SequenceEqual(_outputTempBuffer.AsSpan(0, blueprint.OutputBufferRaw.Length)))
            {
                _playState = PlayButtonState.Play;
            }
        }
    }

    private bool UiInput()
    {
        Rectangle sidebar = Sidebar;

        if (_dragStartUi is not null && !MouseButton.Left.Down())
        {
            // implicit falling edge
            // drag and drop
            if(_dragReason == DragReason.Component)
            {
                if(!sidebar.Contains(InputHelper.MouseLocation))
                {
                    ComponentEntry toAdd = _components[_selectedComponentToPlace];

                    if(CurrentEntry is { Custom: Simulation sim } componentEntry)
                    {
                        sim.Place(toAdd.Blueprint, GetTileOver(), _rotation & 3);
                        ResetSimulation();
                    }

                    _selectedComponentToPlace = -1;
                }
                else
                {
                    int index = EnumerateEntries().TakeWhile(e => !e.Rectangle.Contains(InputHelper.MouseLocation)).Count();
                    if(index < _components.Count)
                    {
                        _selectedSim = index;

                        if (_components[index].Custom is Simulation simulation)
                        {
                            _camera.Position = new Vector2(Step * simulation.Width, Step * simulation.Height) * -0.5f + new Vector2(Step * 0.5f)
                                + Vector2.UnitX * _windowSize.X * 0.5f;
                        }
                    }
                }
            }

            _dragStartUi = null;
            return true;
        }

        if (_dragStartUi is null)
        {
#if !BLAZORGL
            Mouse.SetCursor(MouseCursor.Arrow);
#endif
            Rectangle rightBar = new Rectangle(sidebar.Right, sidebar.Top, 0, sidebar.Height);
            rightBar.Inflate(6, 6);

            Rectangle bottomBar = new Rectangle(sidebar.Left, sidebar.Bottom, sidebar.Width, 0);
            bottomBar.Inflate(6, 6);

            MouseCursor? cursor;
            (_dragReason, cursor) = true switch
            {
                _ when rightBar.Contains(InputHelper.MouseLocation) && bottomBar.Contains(InputHelper.MouseLocation) => (DragReason.Right | DragReason.Bottom, MouseCursor.SizeNWSE),
                _ when rightBar.Contains(InputHelper.MouseLocation) => (DragReason.Right, MouseCursor.SizeWE),
                _ when bottomBar.Contains(InputHelper.MouseLocation) => (DragReason.Bottom, MouseCursor.SizeNS),
                _ when EnumerateEntries().Any(e => e.Rectangle.Contains(InputHelper.MouseLocation)) => (DragReason.Component, default),
                _ => default,
            };
#if !BLAZORGL

            if (cursor is not null)
                Mouse.SetCursor(cursor);
#endif

            if (_dragReason != default && MouseButton.Left.RisingEdge())
            {
                _dragStartUi = InputHelper.MouseLocation;
                if(_dragReason == DragReason.Component)
                {
                    _selectedComponentToPlace = EnumerateEntries().TakeWhile(e => !e.Rectangle.Contains(InputHelper.MouseLocation)).Count();

                    if(_selectedComponentToPlace == _selectedSim)
                    {
                        _dragStartUi = null;
                    }
                }

                return true;
            }
        }

        if(_dragStartUi is not null)
        {
            Vector2 target = InputHelper.MouseLocation.ToVector2() - new Vector2(Padding);

            if (_dragReason.HasFlag(DragReason.Right)) _windowSize.X = target.X;
            if (_dragReason.HasFlag(DragReason.Bottom)) _windowSize.Y = target.Y;
            _windowSize.X = float.Max(_windowSize.X, 20);
            _windowSize.Y = float.Max(_windowSize.Y, 20);

            if(_dragReason == DragReason.Component && Keys.Space.RisingEdge())
            {
                _rotation++;
            }

            return true;
        }

        if(Play.Contains(InputHelper.MouseLocation) && MouseButton.Left.RisingEdge() && CurrentEntry is not null)
        {
            switch(_playState)
            {
                case PlayButtonState.Play:
                    _playState = PlayButtonState.Pause;
                    _testCaseTimer = 0;
                    CurrentTestCaseIndex = 0;
                    _components[_selectedSim].Blueprint.Custom?.ClearAllDelayValues();
                    TestTestCase();
                    break;
                case PlayButtonState.Pause:
                    _playState = PlayButtonState.Play;
                    break;
            }
        }
        
        if(sidebar.Contains(InputHelper.MouseLocation))
        {
            return true;
        }

        return false;
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
            input = true;
            _dragStartWorld = null;
        }

        if(MouseButton.Left.Down() && _dragStartWorld is Point && _simDragReason == SimDragReason.MoveComponent)
        {
            sim.MoveComponent(_draggedComponentId, tileOver);
            //ResetSimulation();
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

        if (CurrentEntry is { } entry)
        {
            entry.Custom?.Draw(_graphics, Step);

            if (_dragReason == DragReason.Component && 
                (uint)_selectedComponentToPlace < (uint)_components.Count && 
                _dragStartUi is not null &&
                !Sidebar.Contains(InputHelper.MouseLocation))
            {
                var toDraw = _components[_selectedComponentToPlace];
                toDraw.Blueprint.Draw(_graphics, null, GetTileOver(), Step, Constants.WireRad, false, rotationOverride: _rotation);
            }

            var over = GetTileOver();
            if (_dragStartWorld is Point dragStart
                && (entry.Custom?.InRange(over) ?? false)
                && over != dragStart
                && _simDragReason == SimDragReason.PlaceWire)
            {
                var colors = Constants.GetWireColor(PowerState.OffState);
                CurrentEntry?.Custom?.DrawWire(_sb, Step, new Wire(dragStart, over), colors.Color * 0.5f, colors.Output * 0.5f);
            }
        }

        _sb.End();
        _graphics.SpriteBatch.End();
        _graphics.SpriteBatchText.End();

        // ui
        _sb.Begin();
        _graphics.SpriteBatchText.Begin(samplerState: SamplerState.PointClamp);

        var sidebar = Sidebar;
        if (_selectedSim < 0)
            _graphics.DrawStringCentered("No Component Selected", _graphics.GraphicsDevice.Viewport.Bounds.Size.ToVector2() * 0.5f);
        else
            _graphics.DrawString(_components[_selectedSim].Name, new Vector2(sidebar.Right + Padding, sidebar.Top), default, 2, Color.White);

        DrawUi();

        _sb.End();
        _graphics.SpriteBatchText.End();
    }

    private Vector2 _windowSize;

    const int Padding = 10;
    const int Rounding = 10;

    private void DrawUi()
    {
        Color dark = new Color(33, 24, 24);
        Color light = new Color(92, 62, 62);

        _sb.DrawRectangle(new(Padding), _windowSize,
            dark,
            light, 4, Rounding);

        int index = 0;
        foreach(var (bounds, entry) in EnumerateEntries())
        {
            _sb.FillRectangle(bounds.Location.ToVector2(), bounds.Size.ToVector2(),
                light * (bounds.Contains(InputHelper.MouseLocation) || index == _selectedComponentToPlace ? 1.2f : 1f), Rounding);

            _graphics.DrawStringCentered(entry.Name, bounds.Center.ToVector2(), color: _components[index].TestCaseIndex switch
            {
                int i when i == _components[index].TestCases?.Length => Color.Green,
                int i when i > 0 && _components[index].TestCases is not null => Color.Red,
                _ => Color.White,
            });
            index++;
        }

        if(_selectedSim == -1)
        {
            return;
        }

        var component = _components[_selectedSim];

        if (component.TestCases is null)
            return;

        Rectangle p = Play;
        float m = p.Contains(InputHelper.MouseLocation) ? MouseButton.Left.Down() ? 1.2f : 1.1f : 1f;

        _sb.DrawRectangle(p.Location.ToVector2(), p.Size.ToVector2(), dark * m, light * m, 4, Rounding);
        switch(_playState)
        {
            case PlayButtonState.Play:
                _sb.DrawEquilateralTriangle(p.Center.ToVector2() - Vector2.UnitX * 3, 12, Color.Green * m, Color.DarkGreen * m, 3, 4, -MathHelper.PiOver2);
                break;
            case PlayButtonState.Pause:
                var size = new Vector2(12, 35);
                _sb.DrawRectangle(p.Center.ToVector2() - size * 0.5f + Vector2.UnitX * -10f, size, Color.Green * m, Color.DarkGreen * m, 2, 4);
                _sb.DrawRectangle(p.Center.ToVector2() - size * 0.5f + Vector2.UnitX * 10f, size, Color.Green * m, Color.DarkGreen * m, 2, 4);
                // when the button is the pause button, we are playing, so display additional info

                break;
        }

        _graphics.DrawStringCentered($"{CurrentTestCaseIndex}/{component.TestCases.Length} passed", new Vector2(p.Left - p.Width, p.Center.Y));
    }

    private IEnumerable<(Rectangle Rectangle, ComponentEntry ComponentEntry)> EnumerateEntries()
    {
        int cardWidth = (int)_windowSize.X - 2 * Padding;
        int cardHeight = (int)(cardWidth * 0.5f);
        Rectangle template = new Rectangle(new(Padding * 2), new(cardWidth, cardHeight));
        foreach (var entry in _components)
        {
            yield return (template, entry);
            template.Offset(0, cardHeight + Padding);
        }
    }

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


    public void OnEnter(IScreen previous, object? args) { }
    public object? OnExit() => null;
}
