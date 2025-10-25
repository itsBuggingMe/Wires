using Microsoft.VisualBasic;
using Paper.Core;
using System.Collections.Generic;
using Wires.Core.Campaigns;
using Wires.Core.Sim;
using Wires.Core.UI;
using Wires.States;

namespace Wires.Core.States;

internal class CampaignState : IScreen
{
    public EditorUI EditorUI => _editorUI;

    private readonly EditorUI _editorUI;
    private readonly SimInteraction _interaction;

    public ComponentEditorResult? EditorOutput;

    public CampaignState(ServiceContainer serviceContainer, Graphics graphics)
    {
        _interaction = new SimInteraction(graphics);
        _editorUI = new EditorUI(graphics, _interaction);


        Campaign[] campaigns = [
            new Day3(serviceContainer, graphics, _editorUI, _interaction),
            new Day2(serviceContainer, graphics, _editorUI, _interaction),
            new Day1(serviceContainer, graphics, _editorUI, _interaction),
            new Sandbox(serviceContainer, this, graphics, _editorUI, _interaction),
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
        if(args is ComponentEditorResult e)
        {
            EditorOutput = e;
        }
    }

    public object? OnExit() => null;
}
