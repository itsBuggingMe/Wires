using Microsoft.VisualBasic;
using Paper.Core;
using System.Collections.Generic;
using Wires.Core.Campaigns;
using Wires.Core.Sim;
using Wires.Core.UI;

namespace Wires.Core.States;

internal class CampaignState : IScreen
{
    private readonly EditorUI _editorUI;
    private readonly SimInteraction _interaction;

    public CampaignState(ServiceContainer serviceContainer, Graphics graphics)
    {
        _interaction = new SimInteraction(graphics);
        _editorUI = new EditorUI(graphics, _interaction);


        Campaign[] campaigns = [
            new Day1(serviceContainer, graphics, _editorUI, _interaction),
            new Day2(serviceContainer, graphics, _editorUI, _interaction),
        ];

        foreach (var campaign in campaigns)
            _editorUI.Campaigns.Add(campaign);
    }


    public void Update(Time gameTime)
    {
        _editorUI.CurrentCampaign?.Update(gameTime);
    }

    public void Draw(Time gameTime)
    {
        _editorUI.CurrentCampaign?.Draw(gameTime);
    }

    public void OnEnter(IScreen previous, object? args)
    {
        
    }

    public object? OnExit() => null;
}
