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
using Wires.Core.Sim.Saving;
using Wires.Core.Sim;
using System.Text.Json;
using MonoGameGum;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Gum.Wireframe;
using MonoGameGum.Input;
using RenderingLibrary;
using System.IO;
using Gum.DataTypes.Variables;
using System.Threading;
using Gum.Forms;
using Paper.Core;

namespace Wires.States;

partial class MainSimulation
{
    private Rectangle Play => new Rectangle(_graphics.GraphicsDevice.Viewport.Width - 64 - Padding, Padding, 64, 64);
    private StackPanel? _rlickMenu;

    // we call InitUi
#pragma warning disable CS8618
    private MainSimulation(Graphics graphics, Camera2D camera, ScreenManager screenManager)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        _graphics = graphics;
        _camera = camera;
        _sb = graphics.ShapeBatch;
        _screenManager = screenManager;

        InitUi(screenManager, graphics);

        graphics.Game.Window.ClientSizeChanged += (o, e) =>
        {
            GumService.Default.Root.Children.Clear();

            InitUi(screenManager, graphics);
            foreach(var component in _components)
            {
                AddComponent(component);
            }
        };
    }


    private void InitUi(ScreenManager screenManager, Graphics graphics)
    {
        const int Padding = 4;

        var gum = GumService.Default;

        var menu = new Menu()
        {
            Y = graphics.GraphicsDevice.Viewport.Height - 26,
            Width = graphics.GraphicsDevice.Viewport.Width
        };
        menu.AddToRoot();

        MenuItem("New Component", () => screenManager.SwitchScreen(new ComponentEditor(this, graphics, screenManager)));
        MenuItem("Load", () => Levels.LoadLocalData((Action<string>)(s =>
        {
            LevelModel[] models = JsonSerializer.Deserialize<LevelModel[]>(s) ?? [];
            this._components.Clear();
            for (int i = _componentButtons.Children.Count - 1; i >= 0; i--)
            {
                _componentButtons.Children[i].RemoveFromRoot();
            }

            AddComponent((ComponentEntry)new(Blueprint.NAND));
            AddComponent((ComponentEntry)new(Blueprint.Delay));
            AddComponent((ComponentEntry)new(Blueprint.On));
            AddComponent((ComponentEntry)new(Blueprint.Off));
            AddComponent((ComponentEntry)new(Blueprint.Switch));
            AddComponent((ComponentEntry)new(Blueprint.Splitter));
            AddComponent((ComponentEntry)new(Blueprint.Joiner));
            AddComponent((ComponentEntry)new(Blueprint.Disp));

            foreach (var i in Levels.CreateComponentEntriesFromModels((List<ComponentEntry>)this._components, models))
            {
                AddComponent(i);
            }
            ResetSimulation();
        })));
        MenuItem("Save", () => Levels.SaveLocalData(Levels.SerializeComponentEntries(_components)));

        void MenuItem(string header, Action onClicked)
        {
            var item = new MenuItem
            {
                Header = header
            };
            menu.Items.Add(item);
            item.Clicked += (o, e) => onClicked();
        }

        var mainPanel = new StackPanel()
        {

        };
        mainPanel.AddToRoot();

        _componentScroller = new ScrollViewer()
        {
            Height = graphics.GraphicsDevice.Viewport.Height - menu.ActualHeight,
        };

        _componentButtons = new StackPanel()
        {
            Width = -Padding,
        };
        _componentButtons.Visual.StackSpacing = Padding;

        _componentScroller.AddChild(_componentButtons);
        mainPanel.AddChild(_componentScroller);

        _componentButtons.Dock(Dock.FillHorizontally);
    }

    private void AddComponent(ComponentEntry componentEntry)
    {
        const int Padding = 4;

        if(!_components.Contains(componentEntry))
            _components.Add(componentEntry);

        Button b = new Button()
        {
            Text = componentEntry.Name,
            Width = -Padding,
        };

        b.Click += (o, e) =>
        {
            _currentEntry = componentEntry;
            if(_currentEntry.Custom is { } simulation)
            {
                _camera.Position = new Vector2(Step * simulation.Width, Step * simulation.Height) * -0.5f + new Vector2(Step * 0.5f)
                    + Vector2.UnitX * (_componentScroller.AbsoluteLeft + _componentScroller.ActualWidth * 0.5f);
            }
        };

        b.Visual.RightClick += (o, e) =>
        {
            var rclickMenu = new StackPanel
            {
                Width = 100,
                X = InputHelper.MouseLocation.X,
                Y = InputHelper.MouseLocation.Y
            };

            var deleteButton = new Button
            {
                Text = "Delete",
            };
            deleteButton.Click += (o, e) =>
            {
                _components.Remove(componentEntry);
                if(_currentEntry == componentEntry)
                {
                    _currentEntry = null;
                }

                for (int i = _componentButtons.Children.Count - 1; i >= 0; i--)
                {
                    _componentButtons.Children[i].RemoveFromRoot();
                }

                foreach (var entry in _components)
                    AddComponent(entry);

                rclickMenu.RemoveFromRoot();

                _rlickMenu = null;
            };
            rclickMenu.AddChild(deleteButton);
            deleteButton.Dock(Dock.FillHorizontally);

            rclickMenu.AddToRoot();

            _rlickMenu = rclickMenu;
        };

        b.Visual.Dragging += (o, e) =>
        {
            _componentToPlace = componentEntry;
        };

        _componentButtons.AddChild(b);
        b.Dock(Dock.FillHorizontally);
    }

    private bool UpdateUi()
    {
        if ((MouseButton.Left.FallingEdge() || MouseButton.Right.FallingEdge()) && _rlickMenu is not null)
        {
            Rectangle bounds = new Rectangle((int)_rlickMenu.AbsoluteLeft, (int)_rlickMenu.AbsoluteTop, (int)_rlickMenu.ActualWidth, (int)_rlickMenu.ActualHeight);
            if(!bounds.Contains(InputHelper.MouseLocation))
            {
                _rlickMenu.RemoveFromRoot();
            }
        }

        if (!MouseButton.Left.Down())
        {
            if(_componentToPlace is not null)
            {
                _currentEntry?.Custom?.Place(_componentToPlace.Blueprint, GetTileOver(), _rotation);
                ResetSimulation();
                if(!InputHelper.Down(Keys.LeftShift))
                {
                    _componentToPlace = null;
                }
            }
        }

        if (_componentToPlace is not null)
        {
            if(InputHelper.FallingEdge(Keys.Space))
            {
                _rotation++;
            }

            return true;
        }

        if (Play.Contains(InputHelper.MouseLocation) && MouseButton.Left.RisingEdge() && _currentEntry is not null)
        {
            switch (_playState)
            {
                case PlayButtonState.Play:
                    _playState = PlayButtonState.Pause;
                    _testCaseTimer = 0;
                    CurrentTestCaseIndex = 0;
                    _currentEntry.Blueprint.Custom?.ClearAllDelayValues();
                    TestTestCase();
                    break;
                case PlayButtonState.Pause:
                    _playState = PlayButtonState.Play;
                    break;
            }
        }

        return false;
    }

    private ComponentEntry? _componentToPlace;

    private StackPanel _componentButtons;
    private ScrollViewer _componentScroller;

    private void DrawUi()
    {
        Color dark = new Color(33, 24, 24);
        Color light = new Color(92, 62, 62);

        _sb.Begin();
        _graphics.SpriteBatchText.Begin(samplerState: SamplerState.PointClamp);

        try
        {
            if (_currentEntry is null)
            {
                _graphics.DrawStringCentered("No Component Selected", _graphics.GraphicsDevice.Viewport.Bounds.Size.ToVector2() * 0.5f);
                return;
            }
            else
                _graphics.DrawString(_currentEntry.Name, new Vector2(_componentScroller.ActualWidth + _componentScroller.AbsoluteLeft + Padding, _componentScroller.Y + Padding), default, 2, Color.White);

            var component = _currentEntry;

            Rectangle p = Play;
            float m = p.Contains(InputHelper.MouseLocation) ? MouseButton.Left.Down() ? 1.2f : 1.1f : 1f;

            _sb.DrawRectangle(p.Location.ToVector2(), p.Size.ToVector2(), dark * m, light * m, 4, Rounding);
            switch (_playState)
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

            if (component.TestCases is not null)
            {
                _graphics.DrawStringCentered($"{CurrentTestCaseIndex}/{component.TestCases.Length} passed", new Vector2(p.Left - p.Width, p.Center.Y));
            }
        }
        finally
        {
            _sb.End();
            _graphics.SpriteBatchText.End();
        }
    }

}
