using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wires.Core;

public class Camera2D
{
    public Matrix View
    {
        get
        {
            Vector3 origin = new Vector3(NormalizedOrigin.X * _graphicsDevice.Viewport.Width,
                             NormalizedOrigin.Y * _graphicsDevice.Viewport.Height, 0);

            Matrix view =
                Matrix.CreateScale(Scale.X, Scale.Y, 1) *
                Matrix.CreateRotationZ(-Rotation) *
                Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
                Matrix.CreateTranslation(origin);

            return view;
        }
    }

    public Matrix Projection => Matrix.CreateOrthographicOffCenter(
        _graphicsDevice.Viewport.X, 
        _graphicsDevice.Viewport.Width, 
        _graphicsDevice.Viewport.Height, 
        _graphicsDevice.Viewport.Y, 0, 1);

    private readonly GraphicsDevice _graphicsDevice;

    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public Vector2 NormalizedOrigin { get; set; }
    public float Rotation { get; set; }

    public Camera2D(GraphicsDevice graphicsDevice, Vector2? scale = null, Vector2? originNorm = null)
    {
        _graphicsDevice = graphicsDevice;
        Scale = scale ?? Vector2.One;
        NormalizedOrigin = originNorm ?? new(0.5f);
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Vector3 worldPos3 = Vector3.Transform(new Vector3(screenPosition, 0), Matrix.Invert(View));

        return new Vector2(worldPos3.X, worldPos3.Y);
    }

}
