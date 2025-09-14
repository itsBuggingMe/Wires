using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameGum;
using MonoGameGum.ExtensionMethods;
using MonoGameGum.GueDeriving;
using RenderingLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Wires.Core;
using Wires.Sim;
using Wires.Sim.Saving;

namespace Wires.States;

internal class ComponentEditor : IScreen
{
    private static readonly Simulation _emptySim = new Simulation(0, 0);
    private static readonly Blueprint _inputTile = new Blueprint([(Point.Zero, TileKind.Input)], string.Empty);
    private static readonly Blueprint _outputTile = new Blueprint([(Point.Zero, TileKind.Output)], string.Empty);
    private static readonly Blueprint _componentTile = new Blueprint([(Point.Zero, TileKind.Component)], string.Empty);

    private readonly MainSimulation _return;
    private readonly Graphics _graphics;
    private readonly Camera2D _camera;
    private readonly ScreenManager _screenManager;
    private Label? _errorLabel;

    private int _width = 9;
    private int _height = 9;
    private string _name = "NAME";
    private Simulation _displaySim;

    private static readonly ImmutableArray<Point> Deltas = [new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1), new(0, -1), new(1, -1)];

    private readonly Dictionary<Point, TileKind> _placedTiles = [];
    private readonly List<Point> _inputs = [];
    private readonly List<Point> _outputs = [];

    private int? _componentId;

    private TileKind? _placedTileKind;

    const int SimSize = 48;

    public ComponentEditor(MainSimulation @return, Graphics graphics, ScreenManager screenManager)
    {
        _return = @return;
        _graphics = graphics;
        _camera = graphics.Camera;
        _screenManager = screenManager;

        _placedTiles.Add(default, TileKind.Component);

        _displaySim = new Simulation(SimSize, SimSize);

        graphics.Camera.Position = new Vector2(50) * -(SimSize / 2);

        UpdateDummyComponent();
    }

    // TODO: refactor camera and tile utils out
    private Point GetTileOver() => ((_camera.ScreenToWorld(InputHelper.MouseLocation.ToVector2()) - new Vector2(-50 / 2)) / new Vector2(50)).ToPoint();

    public void Update(Time gameTime)
    {
        if (InputHelper.RisingEdge(MouseButton.Left) || InputHelper.RisingEdge(MouseButton.Right))
            SetErrorText();

        if (GumService.Default.Cursor.WindowOver != null)
            return;

        var p = GetTileOver() - new Point(SimSize / 2);
        if(_placedTileKind is { } tile)
        {

            if(!MouseButton.Left.Down())
            {
                // place
                if(Deltas.Any(o => _placedTiles.TryGetValue(o + p, out var kind) && kind is TileKind.Component) && !_placedTiles.ContainsKey(p))
                {
                    _placedTiles[p] = tile;
                    if (tile is TileKind.Input)
                        _inputs.Add(p);
                    else if (tile is TileKind.Output)
                        _outputs.Add(p);
                }
                else
                {
                    if(_placedTiles.ContainsKey(p))
                    {
                        SetErrorText("Already a tile here.");
                    }
                    else
                    {
                        SetErrorText("Tiles must be placed near a component tile.");
                    }
                }
                UpdateDummyComponent();
                _placedTileKind = null;
            }
            return;
        }

        if(InputHelper.Down(MouseButton.Right))
        {
            if (p == default)
                return;
            if(_placedTiles.TryGetValue(p, out var kind))
            {
                _placedTiles.Remove(p);
                _inputs.Remove(p);
                _outputs.Remove(p);

                if (kind == TileKind.Component)
                {
                    HashSet<Point> visited = [default];
                    FloodFill(default);
                    Point[] allPoints = _placedTiles.Select(kvp => kvp.Key).ToArray();

                    foreach(var p1 in allPoints)
                    {
                        if (!visited.Contains(p1))
                        {
                            _placedTiles.Remove(p1);
                            _inputs.Remove(p1);
                            _outputs.Remove(p1);
                        }
                    }

                    void FloodFill(Point p)
                    {
                        foreach (var (neighbor, kind) in Deltas.Select(o => _placedTiles.TryGetValue(p + o, out var kind) ? (p + o, kind) : (new(int.MinValue), default)).Where(t => t.Item1.X != int.MinValue))
                        {
                            if (!visited.Add(neighbor))
                                continue;

                            if (kind is TileKind.Component)
                            {
                                FloodFill(neighbor);
                            }
                        }
                    }
                }

                UpdateDummyComponent();
            }

            return;
        }

        const float ScrollScaleM = 1.2f;
        if (InputHelper.DeltaScroll != 0)
            _camera.Scale *= InputHelper.DeltaScroll > 0 ? ScrollScaleM : 1 / ScrollScaleM;
        if (Keys.OemPlus.RisingEdge())
            _camera.Scale *= ScrollScaleM;
        if (Keys.OemMinus.RisingEdge())
            _camera.Scale /= ScrollScaleM;

        if (MouseButton.Left.Down())
            _camera.Position -= (InputHelper.PrevMouseState.Position.ToVector2() - InputHelper.MouseLocation.ToVector2()) / _camera.Scale;
    }

    private void UpdateDummyComponent()
    {
        if (_componentId is not null)
            _displaySim.DestroyComponent(new Point(SimSize / 2));

        Blueprint blueprint = new Blueprint(_emptySim, _name, _placedTiles.Select(t => (t.Key, t.Value)).ToImmutableArray());

        _componentId = _displaySim.Place(blueprint, new Point(SimSize / 2), 0);
    }

    private void SetErrorText(string str = "")
    {
        if(_errorLabel is not null)
        {
            _errorLabel.Text = str;
        }
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);

        _graphics.ShapeBatch.Begin(_graphics.Camera.View, _graphics.Camera.Projection);
        _graphics.SpriteBatch.Begin(transformMatrix: _graphics.Camera.View);
        _graphics.SpriteBatchText.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _graphics.Camera.View);


        _displaySim.Draw(_graphics, 50);

        if(_placedTileKind is TileKind kind)
        {
            (kind switch
            {
                TileKind.Input => _inputTile,
                TileKind.Output => _outputTile,
                TileKind.Component => _componentTile,
                _ => throw new Exception()
            }).Draw(_graphics, null, GetTileOver(), 50, Constants.WireRad, false, 0);
        }

        for(int i = 0; i < _inputs.Count; i++)
        {
            DrawTextAtTileOffset(_inputs[i], $"IN {i}");
        }
        for (int i = 0; i < _outputs.Count; i++)
        {
            DrawTextAtTileOffset(_outputs[i], $"OUT {i}");
        }

        void DrawTextAtTileOffset(Point point, string text) => _graphics.DrawStringCentered(text, (point + new Point(SimSize / 2)).ToVector2() * new Vector2(50) + new Vector2(0, -17));

        _graphics.ShapeBatch.End();
        _graphics.SpriteBatch.End();
        _graphics.SpriteBatchText.End();
    }

    public void OnEnter(IScreen previous, object? args)
    {
        var panel = new StackPanel();
        panel.Spacing = 5;
        panel.X = 20;
        panel.Y = 20;
        panel.AddToRoot();

        var nameTextBox = new TextBox
        {
            Text = _name,
            Placeholder = "Component Name",
            Width = 200,
        };
        nameTextBox.TextChanged += (o, e) =>
        {
            _name = nameTextBox.Text;
            UpdateDummyComponent();
        };
        panel.AddChild(nameTextBox);

        AddHeightWidthBox("Width", i => _width = i);
        AddHeightWidthBox("Height", i => _height = i);

        TextBox AddHeightWidthBox(string placeholder, Action<int> set)
        {
            panel.AddChild(new Label
            {
                Text = placeholder
            });

            var box = new TextBox
            {
                Text = "9",
                Placeholder = placeholder,
                Width = 200,
            };
            box.PreviewTextInput += 
                (s, e) => e.Handled =  e.Text.Any(t => !char.IsDigit(t));
            box.TextChanged += (o, e) => set(box.Text.Length == 0 ? 0 : int.Parse(box.Text));
            panel.AddChild(box);

            return box;
        }

        AddTileKindButton("Component Tile", TileKind.Component);
        AddTileKindButton("Input Tile", TileKind.Input);
        AddTileKindButton("Output Tile", TileKind.Output);

        var doubleButton = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var saveButton = new Button
        {
            Width = 95,
            Height = 20,
            Text = "Save",
        };

        saveButton.Click += (o, e) =>
        {
            SetErrorText(string.Empty);
            if (_width < 4)
            {
                SetErrorText("Invalid Width Value (Must be >= 4)");
                return;
            }
            if (_height < Math.Max(_inputs.Count, _outputs.Count))
            {
                SetErrorText("Invalid Height Value (Not enough space for inputs/outputs)");
                return;
            }
            if (string.IsNullOrWhiteSpace(_name))
            {
                SetErrorText("Invalid Name Value");
                return;
            }
            if (_return.Components.Any(c => c.Name == _name))
            {
                SetErrorText($"Component with name {_name} already exists.");
                return;
            }

            _screenManager.SwitchScreen(_return);
        };

        var cancelButton = new Button
        {
            X = 10,
            Width = 95,
            Height = 20,
            Text = "Cancel",
        };

        cancelButton.Click += (o, e) =>
        {
            _isCancel = true;
            _screenManager.SwitchScreen(_return);
        };

        doubleButton.AddChild(saveButton);
        doubleButton.AddChild(cancelButton);
        panel.AddChild(doubleButton);

        _errorLabel = new Label
        {
            Text = "",
        };
        ((LabelVisual)_errorLabel.Visual).Color = Color.Red;

        panel.AddChild(_errorLabel);

        void AddTileKindButton(string buttonText, TileKind tileKind)
        {
            var button = new Button
            {
                Width = 200,
                Height = 50,
                Text = buttonText,
            };

            button.Visual.Dragging += (s, e) =>
            {
                _placedTileKind = tileKind;
            };

            panel.AddChild(button);
        }
    }

    private bool _isCancel;

    public object? OnExit()
    {
        GumService.Default.Root.Children.Clear();

        if (_isCancel)
            return new ComponentEditorResult(null);

        return new ComponentEditorResult(new LevelModel
            {
                Name = _name,
                ComponentTiles = _placedTiles.OrderBy(i => _inputs.IndexOf(i.Key) is int x && x != -1 ? x : _outputs.IndexOf(i.Key)).Select(kvp => new ComponentTileModel
                {
                    X = kvp.Key.X,
                    Y = kvp.Key.Y,
                    TileKind = kvp.Value.ToString(),
                }).ToArray(),
                GridHeight = _height,
                GridWidth = _width,
                TestCases = [],
                Components = Enumerable.Concat(_inputs.Select((p, i) => new ComponentModel
                {
                    BlueprintName = nameof(Blueprint.Input),
                    AllowDelete = false,
                    InputOutputId = i,
                    Rotation = 0,
                    X = 0,
                    Y = i,
                }), _outputs.Select((p, i) => new ComponentModel
                {
                    BlueprintName = nameof(Blueprint.Output),
                    AllowDelete = false,
                    InputOutputId = i,
                    Rotation = 0,
                    X = 3,
                    Y = i,
                })).ToArray(),
            });
    }
}

record class ComponentEditorResult(LevelModel? ResultModel);