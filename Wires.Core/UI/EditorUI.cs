using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Paper.Core;
using Paper.Core.UI;
using System;
using System.Collections.ObjectModel;
using Wires.Core.Sim;
using Align = Paper.Core.UI.ElementAlign;

namespace Wires.Core.UI;

internal class EditorUI : RootUI<Graphics>
{
    const int Padding = Constants.Padding;

    public StackPanel Menu  => _menu;
    public Button AddButton => _addButton;
    public Button PlayButton => _playButton;
    public Button NextButton => _nextButton;
    public StackPanel AddMenu => _addMenu;
    public StackPanel CampaignsMenu => _campaignsMenu;
    public Button NewComponentButton => _chip;

    private readonly StackPanel _menu;
    private readonly Button _addButton;
    private readonly Button _playButton;
    private readonly Button _nextButton;
    private readonly StackPanel _addMenu;
    private readonly StackPanel _campaignsMenu;
    private readonly StackPanel _levelsMenu;
    private readonly Button _chip;

    private ShortCircuitTooltip? _ssTooltip;

    private TruthTable? _currentDisplayTable;

    public SimInteraction Interaction { get; private set; }
    public Campaign? CurrentCampaign => _currentCampaign;

    public bool IsPlaying { get => _isPlaying; set => _isPlaying = value; }


    private bool _isPlaying = false;
    private int _testCaseIndex = 0;

    public readonly ObservableCollection<ComponentEntry> PlaceableComponents = [];
    public readonly ObservableCollection<Campaign> Campaigns = [];
    public readonly ObservableCollection<ComponentEntry> Levels = [];

    public ComponentEntry? CurrentEntry => _currentCampaign?.Interaction.ActiveEntry;
    private Campaign? _currentCampaign;

    private PowerState[] _tempPowerStateBuffer = new PowerState[128];

    public Action<Campaign>? CampaignSelected { get; set; }

    public int TestCaseIndex
    {
        get => _testCaseIndex;
        set => _testCaseIndex = value;
    }

    public EditorUI(Graphics graphics, SimInteraction interaction) : base(graphics.Game, graphics, 1920, 1080)
    {
        Interaction = interaction;
        Children = [
        // pancake menu
        new Button(new(Padding), new(48, false), Button.Pancake)
            {
                Children = [_menu = new StackPanel(new(48, Padding), new UIVector2(40), false)
                {
                    Children = [
                        new Button(default, new Vector2(100, 40), g => { })
                        {
                            Children = [_levelsMenu = new StackPanel(new(48 * 2 + Padding, -Padding / 2), new UIVector2(40), true)
                            {
                                ElementAlign = Align.TopLeft,
                                Visible = false,
                                ColorMultipler = 0,
                            }, new Text(new(50, 20), "Levels") { ElementAlign = Align.Center }],
                            Clicked = _ => {
                                _levelsMenu.Visible = !_levelsMenu.Visible;
                                _campaignsMenu!.Visible = false;
                            },
                        },
                        new Button(default, new Vector2(100, 40), g => { })
                        {
                            Children = [_campaignsMenu = new StackPanel(new(48 * 2 + Padding, -Padding / 2), new UIVector2(40), true)
                            {
                                ElementAlign = Align.TopLeft,
                                Visible = false,
                                ColorMultipler = 0,
                            }, new Text(new(50, 20), "Days") { ElementAlign = Align.Center }
                            ],
                            Clicked = _ => {
                                _campaignsMenu.Visible = !_campaignsMenu.Visible;
                                _levelsMenu.Visible = false;
                            },
                        },],
                    Visible = false,
                }],
                Clicked = p => {
                    _addMenu!.Visible = false;
                    _menu.Visible = !_menu.Visible;
                }
            },
            _addButton = new Button(new(Padding, 64 + Padding, false, false), new(48, false), Button.Plus)
            {
                Visible = false,
                Children = [_addMenu = new StackPanel(new(48, 0), new UIVector2(40))
                {
                    Visible = false,
                    ColorMultipler = 0,
                }],
                Clicked = p => {
                    _levelsMenu.Visible = false;
                    _campaignsMenu.Visible = false;
                    _menu.Visible = false;
                    _addMenu.Visible = !_addMenu.Visible;
                }
            },
            _chip = new Button(new(Padding, 128 + Padding, false, false), new(48, false), Button.Chip)
            {
                Visible = false,
                Clicked = p => { },
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
                ElementAlign = Align.TopRight,
                Clicked = p =>
                {
                    _isPlaying = !_isPlaying;
                    if(_isPlaying)
                    {
                        _testCaseIndex = 0;
                        CurrentEntry?.TestCases?.Set(TestCaseIndex, CurrentEntry.Blueprint.InputBufferRaw, _tempPowerStateBuffer);
                        Interaction.Step();
                    }
                },
                Children = [
                    new Text(new(-64 - Padding, Padding, false, false), contentSource: () => CurrentEntry switch
                    {
                        { TestCases.Length: 0 } => $"Tick {_testCaseIndex}",
                        { TestCases.Length: > 0 } => $"Test {_testCaseIndex + 1}/{CurrentEntry?.TestCases.Length}",
                        _ => string.Empty,
                    })
                    {
                        ElementAlign = Align.TopRight,
                    }
                ]
            },
            _nextButton = new Button(new(1920 - Padding, 1080 - Padding), new(280, 60), Button.None)
            {
                ElementAlign = Align.BottomRight,
                Text = new Text(new(-140, -30), "Next Level") { ElementAlign = Align.Center },
                Visible = false,
                Clicked = p => CurrentCampaign?.NextButtonClicked?.Invoke(),
            },
            new Text(new(1920 / 2, Padding), contentSource: () => CurrentEntry?.Name)
            {
                ElementAlign = Align.TopMiddle,
                Scale = Vector2.One * 2,
            },
        ];


        PlaceableComponents.CollectionChanged += (s, e) => {
            _addMenu.Children = [];
            foreach (var component in PlaceableComponents)
            {
                _addMenu.AddChild(new Button(default, new Vector2(80, 36), Button.None)
                {
                    Text = new Text(new(40, 18), component.Blueprint.Text) { ElementAlign = Align.Center },
                    RisingEdge = p =>
                    {
                        Interaction?.BeginPlaceComponent(component);
                    }
                });
            }

            _addButton.Visible = PlaceableComponents.Count != 0;
        };

        Campaigns.CollectionChanged += (s, e) => {
            _campaignsMenu.Children = [];
            foreach (var campaign in Campaigns)
            {
                _campaignsMenu.AddChild(new Button(default, new Vector2(120, 40), Button.None)
                {
                    Text = new Text(new(60, 20), campaign.Name) { ElementAlign = Align.Center },
                    Clicked = p => {
                        Interaction?.Reset();
                        _currentCampaign = campaign;
                        CampaignSelected?.Invoke(_currentCampaign);

                        PlaceableComponents.Clear();
                        foreach (var item in campaign.AddMenuItems)
                            PlaceableComponents.Add(item);

                        Levels.Clear();
                        foreach (var item in campaign.LevelItems)
                            Levels.Add(item);

                        _nextButton.Visible = CurrentCampaign?.NextButtonVisible ?? false;
                        _chip.Visible = CurrentCampaign?.ComponentEditorButtonVisible ?? false;

                        if (campaign.LevelItems.Count > 0)
                            SwitchLevel(campaign.LevelItems[0]);

                        _levelsMenu.Visible = false;
                        _campaignsMenu.Visible = false;
                        _menu.Visible = false;
                    }
                });
            }

            if (Campaigns.Count > 0)
            {
                _currentCampaign ??= Campaigns[0];
            }
        };

        Levels.CollectionChanged += (s, e) => {
            _levelsMenu.Children = [];
            foreach (var l in Levels)
            {
                _levelsMenu.AddChild(new Button(default, new Vector2(100, 40), g => { })
                {
                    Text = new Text(new(50, 20), l.Name) { ElementAlign = Align.Center },
                    Clicked = _ => {
                        SwitchLevel(l);
                    },
                });
            }

            if (Levels.Count > 0 && Interaction.ActiveEntry is null)
            {
                SwitchLevel(Levels[0]);
            }
        };
    }

    public override bool Update()
    {
        if(_ssTooltip is null && Interaction.ActiveSim is { CurrentShortCircuit: { } ss })
        {
            _ssTooltip = new ShortCircuitTooltip(default, Interaction.ActiveSim, ss);
            AddChild(_ssTooltip);
        }

        if(_ssTooltip is not null && Interaction.ActiveSim is { CurrentShortCircuit: null })
        {
            RemoveChild(_ssTooltip);
            _ssTooltip = null;
        }

        return base.Update();
    }

    public void SwitchLevel(ComponentEntry entry)
    {
        if (!Levels.Contains(entry))
        {
            Levels.Add(entry);
        }

        _playButton.Visible = entry.TestCases is not null;
        Interaction.ActiveEntry = entry;
        NextButton.Visible = CurrentCampaign?.NextButtonVisible ?? false;
        _testCaseIndex = 0;
        _isPlaying = false;

        _currentDisplayTable?.Remove();

        if (entry.TruthTable is not null)
        {
            _currentDisplayTable = new TruthTable(Graphics, new UIVector2(1920 - Constants.Padding, 96, true, false), entry.TruthTable.Headers, entry.TruthTable.Rows)
            {
                ElementAlign = Align.TopRight
            };
            AddChild(_currentDisplayTable);
        }
    }
}
