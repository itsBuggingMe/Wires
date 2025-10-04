using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Paper.Core;
using Paper.Core.UI;
using System;
using System.Collections.Immutable;
using System.Text;

namespace Wires.Core.UI;

internal class TextInput : BorderedElement
{
    public const int MaxLength = 14;
    public StringBuilder Text { get; set; }

    public string Placeholder { get; set; }

    public Action<StringBuilder>? TextChanged { get; set; }

    public Func<char, bool>? ValidateChar { get;set; }

    private bool _hasFocus;

    private Text _text;

    private int _frames;
    private readonly int _maxChars;

    public TextInput(UIVector2 pos, string? placeholder = null, int maxChars = 16) : base(pos, new UIVector2(195, 32, false, false))
    {
        _maxChars = maxChars;
        Text = new();
        Placeholder = placeholder ?? string.Empty;
        AddChild(_text = new Text(new UIVector2(Constants.Padding, 16, false, false), contentSource: () => Text.Length == 0 ? Placeholder : Text.ToString())
        {
            ElementAlign = Paper.Core.UI.ElementAlign.LeftMiddle,
        });
    }

    public override bool Update()
    {
        _frames++;

        if (InputHelper.FallingEdge(MouseButton.Left))
        {
            _hasFocus = Bounds.Contains(InputHelper.MouseLocation);
        }

        if(_hasFocus)
        {
            if(Text.Length < _maxChars)
            {
                foreach (var (k, c) in CharMap)
                {
                    if (InputHelper.RisingEdge(k))
                    {
                        char toOutput = c;
                        if (char.IsAsciiLetter(toOutput) && Keys.LeftShift.Down() || Keys.RightShift.Down())
                            toOutput = char.ToUpper(toOutput);

                        if (ValidateChar is { } validate && !validate(toOutput))
                            continue;

                        Text.Append(toOutput);
                        TextChanged?.Invoke(Text);
                    }
                }
            }

            if(InputHelper.RisingEdge(Keys.Back) && Text.Length > 0)
            {
                Text.Remove(Text.Length - 1, 1);
                TextChanged?.Invoke(Text);
            }
        }

        _text.Color = Text.Length == 0 ?
            Color.Gray :
            Color.White;

        return base.Update() || Bounds.Contains(InputHelper.MouseLocation);
    }

    public override void Draw()
    {
        base.Draw();
        var bounds = _text.Bounds;

        if((_frames & 63) < 32 && _hasFocus)
        {
            Graphics.ShapeBatch.FillRectangle(Text.Length == 0 ?
                bounds.Location.ToVector2() - new Vector2(2, 0) :
                new Vector2(bounds.Right, bounds.Top), new Vector2(2, bounds.Height), Color.White, aaSize: 0);
        }
    }

    private static readonly ImmutableArray<(Keys Key, char Char)> CharMap =
    [
        (Keys.A , 'a'),
        (Keys.B , 'b'),
        (Keys.C , 'c'),
        (Keys.D , 'd'),
        (Keys.E , 'e'),
        (Keys.F , 'f'),
        (Keys.G , 'g'),
        (Keys.H , 'h'),
        (Keys.I , 'i'),
        (Keys.J , 'j'),
        (Keys.K , 'k'),
        (Keys.L , 'l'),
        (Keys.M , 'm'),
        (Keys.N , 'n'),
        (Keys.O , 'o'),
        (Keys.P , 'p'),
        (Keys.Q , 'q'),
        (Keys.R , 'r'),
        (Keys.S , 's'),
        (Keys.T , 't'),
        (Keys.U , 'u'),
        (Keys.V , 'v'),
        (Keys.W , 'w'),
        (Keys.X , 'x'),
        (Keys.Y , 'y'),
        (Keys.Z , 'z'),

        (Keys.D0 , '0'),
        (Keys.D1 , '1'),
        (Keys.D2 , '2'),
        (Keys.D3 , '3'),
        (Keys.D4 , '4'),
        (Keys.D5 , '5'),
        (Keys.D6 , '6'),
        (Keys.D7 , '7'),
        (Keys.D8 , '8'),
        (Keys.D9 , '9'),

        (Keys.NumPad0 , '0'),
        (Keys.NumPad1 , '1'),
        (Keys.NumPad2 , '2'),
        (Keys.NumPad3 , '3'),
        (Keys.NumPad4 , '4'),
        (Keys.NumPad5 , '5'),
        (Keys.NumPad6 , '6'),
        (Keys.NumPad7 , '7'),
        (Keys.NumPad8 , '8'),
        (Keys.NumPad9 , '9'),

        (Keys.Space , ' ')
    ];
}
