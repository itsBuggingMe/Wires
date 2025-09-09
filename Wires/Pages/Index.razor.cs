using Microsoft.JSInterop;
using Microsoft.Xna.Framework;
using System;
using Wires.Sim.Saving;

namespace Wires.Pages;
public partial class Index
{
    Game _game;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (firstRender)
        {
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
        }
    }

    [JSInvokable]
    public void TickDotNet()
    {
        // init game
        if (_game == null)
        {
            Levels.JSRuntimeInstance = JsRuntime;
            _game = new WiresGame();
            _game.Run();
        }

        // run gameloop
        _game.Tick();
    }

}
