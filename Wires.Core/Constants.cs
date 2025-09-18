using Microsoft.Xna.Framework;
using Wires.Core.Sim;

namespace Wires.Core;

public static class Constants
{
    public static readonly Color Background = new(32, 28, 28);
    public static readonly Color Accent = new(0x2b2b2eu);
    public const int WireRad = 10;

    public static (Color Color, Color Output) GetWireColor(PowerState powerState) => powerState switch
    {
        { On: true } => (Color.Green, Color.DarkGreen),
        { On: false } => (Color.Red, Color.DarkRed),
    };
}
