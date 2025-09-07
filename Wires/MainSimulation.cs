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
using Wires.Sim;

namespace Wires;

#nullable enable

public class MainSimulation : IScreen
{
    private Camera2D _camera;
    private ShapeBatch _sb;
    private Graphics _graphics;

    private Point? _dragStartWorld;
    private Point? _dragStartUi;
    private DragReason _dragReason;
    private int _selectedComponentToPlace = -1;

    private enum DragReason : byte { Right = 1, Bottom = 2, Component = 4, }
    private enum PlayButtonState : byte { Play, Pause }
    private PlayButtonState _playState;
    private int _currentTestCaseIndex;
    private float _testCaseTimer;

    private readonly List<ComponentEntry> _components = [];
    private int _selectedSim = -1;

    private PowerState[] _outputTempBuffer = new PowerState[128];

    private ComponentEntry? CurrentEntry => _selectedSim == -1 ? null : _components[_selectedSim];

    public MainSimulation(Camera2D camera, Graphics graphics)
    {
        _graphics = graphics;
        _camera = camera;
        _sb = graphics.ShapeBatch;

        _components.Add(new(Blueprint.NAND));
        _components.Add(new(Blueprint.Delay));
        //_components.Add(new(Blueprint.On));
        //_components.Add(new(Blueprint.Off));

        _components.AddRange(Levels.LoadLevels(_components));

        _windowSize = new(_graphics.GraphicsDevice.Viewport.Width * 0.1f, _graphics.GraphicsDevice.Viewport.Height - 2 * Padding);
    }

    public void Update(Time gameTime)
    {
        _testCaseTimer += gameTime.FrameDeltaTime;

        if(_testCaseTimer > 30)
        {
            _testCaseTimer -= 30;
            if(_playState == PlayButtonState.Pause && CurrentEntry.HasValue)
            {
                _currentTestCaseIndex++;
                if (_currentTestCaseIndex >= CurrentEntry.Value.TestCases!.Length)
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
        if (UiInput())
            return;

        if (CurrentEntry is { Custom: not null } x && SimInteract(x.Blueprint))
            return;

        UpdateCamera();
    }

    private Rectangle Sidebar => new Rectangle(new(Padding), _windowSize.ToPoint());
    private Rectangle Play => new Rectangle(_graphics.GraphicsDevice.Viewport.Width - 64 - Padding, Padding, 64, 64);


    private void TestTestCase()
    {
        if(CurrentEntry is { TestCases: { } cases, Blueprint: { } blueprint })
        {
            cases.Set(_currentTestCaseIndex, blueprint.InputBufferRaw, _outputTempBuffer);
            blueprint.StepStateful();

            if(!blueprint.OutputBufferRaw.AsSpan().SequenceEqual(_outputTempBuffer.AsSpan(0, blueprint.OutputBufferRaw.Length)))
            {
                _playState = PlayButtonState.Play;
            }
        }
    }

    private bool UiInput()
    {
        Rectangle sidebar = Sidebar;

        if (_dragStartUi is not null && !InputHelper.Down(MouseButton.Left))
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
                        sim.Place(toAdd.Blueprint, GetTileOver());
                        componentEntry.Blueprint.Reset();
                    }

                    _selectedComponentToPlace = -1;
                }
                else
                {
                    int index = EnumerateEntries().TakeWhile(e => !e.Rectangle.Contains(InputHelper.MouseLocation)).Count();
                    if(index < _components.Count)
                    {
                        _selectedSim = index;
                        if(_components[index].Custom is Simulation simulation)
                        {
                            _camera.Position = new Vector2(-Step) * new Vector2(simulation.Width, simulation.Height) / 2 + _graphics.GraphicsDevice.Viewport.Bounds.Size.ToVector2() / 2;
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

            if (_dragReason != default && InputHelper.RisingEdge(MouseButton.Left))
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

            return true;
        }

        if(Play.Contains(InputHelper.MouseLocation) && InputHelper.RisingEdge(MouseButton.Left) && CurrentEntry.HasValue)
        {
            switch(_playState)
            {
                case PlayButtonState.Play:
                    _playState = PlayButtonState.Pause;
                    _testCaseTimer = 0;
                    _currentTestCaseIndex = 0;
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

        if (InputHelper.RisingEdge(MouseButton.Left) && sim.InRange(tileOver) && 
            (sim[tileOver].Kind is TileKind.Output or TileKind.Input || sim.IdOfWireAt(tileOver) is int))
        {
            input = true;
            _dragStartWorld = tileOver;
        }

        if (InputHelper.FallingEdge(MouseButton.Left)
            && _dragStartWorld is Point dragStart
            && sim.InRange(tileOver)
            && tileOver != dragStart)
        {
            input = true;
            sim.CreateWire(new Wire(dragStart, tileOver));
            blueprint.Reset();
            _dragStartWorld = null;
        }

        if (InputHelper.FallingEdge(MouseButton.Right))
        {
            input = true;
            if(sim.IdOfWireAt(tileOver) is int id)
            {
                sim.DestroyWire(id);
                blueprint.Reset();
            }
            else if (sim[tileOver].Kind is not TileKind.Nothing)
            {
                sim.DestroyComponent(tileOver);
            }
        }

        return input;
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);

        _sb.Begin(_camera.View, _camera.Projection);
        _graphics.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.View);

        if (_selectedSim < _components.Count && _selectedSim >= 0)
        {
            _components[_selectedSim].Custom?.Draw(_graphics, Step);

            if (_dragReason == DragReason.Component && 
                (uint)_selectedComponentToPlace < (uint)_components.Count && 
                _dragStartUi is not null &&
                !Sidebar.Contains(InputHelper.MouseLocation))
            {
                var toDraw = _components[_selectedComponentToPlace];
                toDraw.Blueprint.Draw(_graphics, null, GetTileOver(), Step, Constants.WireRad, false);
            }
        }

        _sb.End();
        _graphics.SpriteBatch.End();

        // ui
        _sb.Begin();
        _graphics.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var sidebar = Sidebar;
        if (_selectedSim < 0)
            _graphics.DrawStringCentered("No Component Selected", _graphics.GraphicsDevice.Viewport.Bounds.Size.ToVector2() * 0.5f);
        else
            _graphics.DrawString(_components[_selectedSim].Name, new Vector2(sidebar.Right + Padding, sidebar.Top), default, 2, Color.White);
        DrawUi();

        _sb.End();
        _graphics.SpriteBatch.End();
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
                new Color(92, 62, 62) * (bounds.Contains(InputHelper.MouseLocation) || index == _selectedComponentToPlace ? 1.2f : 1f), Rounding);
            _graphics.DrawStringCentered(entry.Name, bounds.Center.ToVector2());
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
        float m = p.Contains(InputHelper.MouseLocation) ? InputHelper.Down(MouseButton.Left) ? 1.2f : 1.1f : 1f;

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

        _graphics.DrawStringCentered($"{_currentTestCaseIndex}/{component.TestCases.Length} passed", new Vector2(p.Left - p.Width, p.Center.Y));
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

        if (InputHelper.Down(MouseButton.Left) && _dragStartWorld is null)
            _camera.Position -= (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2());
    }

    const float Step = 50f;


    public void OnEnter(IScreen previous, object? args) { }
    public object? OnExit() => null;
}
