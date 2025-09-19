using Paper.Core;
using Paper.Core.UI;
using Wires.Core.UI;
using Microsoft.Xna.Framework;
using System;
using Wires.Core.States;
using System.Collections.Generic;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Campaign1.HotReloadManager))]
namespace Wires.Core.States;

internal class Campaign1 : IScreen
{
    private RootUI<Graphics> _root;
    private List<>
    
    private readonly Graphics _graphics;
    private bool _isPlaying;

    public Campaign1(Graphics graphics)
    {
        _graphics = graphics;

        const int Padding = Constants.Padding;

        _root = CreateRoot();
        HotReloadManager.HotReloadOccured += _ => _root = CreateRoot();

        RootUI<Graphics> CreateRoot() => new(graphics.Game, graphics, 1920, 1080)
        {
            Children =
            [
                new Button(new Vector2(1920, 0) + new Vector2(-Padding, Padding), new(Vector2.One * 48, false), Button.Pancake)
                {
                    ElementAlign = ElementAlign.TopRight,
                    Children =
                    [
                        new Button(new Vector2(-Padding, 0), new(Vector2.One * 64, false), b => {
                            var sb = b.Graphics.ShapeBatch;
                            var p = b.Bounds;

                            if(_isPlaying)
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
                            ElementAlign = ElementAlign.TopRight,
                            Clicked = p => _isPlaying = !_isPlaying,
                        },
                    ],
                },
                //new ScrollViewer(Vector2.One * Padding, new Vector2(200, 1080 - 2 * Padding))
                //{
                //    Children =
                //    [
                //        
                //    ]
                //},
            ],
        };
    }

    public void Update(Time gameTime)
    {
        _root.Update();
    }

    public void Draw(Time gameTime)
    {
        _graphics.GraphicsDevice.Clear(Constants.Background);
        _graphics.StartBatches();
        _root.Draw();
        _graphics.EndBatches();
    }

    public void OnEnter(IScreen previous, object? args) { }
    public object? OnExit() => null;


    public static class HotReloadManager
    {
        public static Action<Type[]?>? HotReloadOccured;

        public static void ClearCache(Type[]? updatedTypes)
        {

        }

        public static void UpdateApplication(Type[]? updatedTypes)
        {
            HotReloadOccured?.Invoke(updatedTypes);
        }
    }
}
