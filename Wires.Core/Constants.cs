using Microsoft.Xna.Framework;
using Wires.Core.Sim;

namespace Wires.Core;

public static class Constants
{
    public static readonly Color Background = new(32, 28, 28);
    public static readonly Color Accent = new(0x2b2b2eu);

    public static readonly Color UIDark = new Color(33, 24, 24);
    public static readonly Color UILight = new Color(92, 62, 62);
    public const int WireRad = 10;

    public const int Padding = 16;
    public const int Scale = 50;

    public static (Color Color, Color Output) BundleWireColor => (new Color(66, 135, 245), new Color(19, 87, 194));

    public static (Color Color, Color Output) GetWireColor(PowerState powerState) => powerState switch
    {
        { On: true } => (Color.Green, Color.DarkGreen),
        { On: false } => (Color.Red, Color.DarkRed),
    };
}
