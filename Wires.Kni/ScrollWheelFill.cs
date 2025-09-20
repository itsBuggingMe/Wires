using Microsoft.JSInterop;
using Paper.Core;

namespace Wires.Kni;

public static class ScrollWheelFill
{
    [JSInvokable]
    public static void ScrollUpdate(int delta)
    {
        InputHelper.CustomScrollValue = (InputHelper.CustomScrollValue ?? 0) + delta;
    }
}
