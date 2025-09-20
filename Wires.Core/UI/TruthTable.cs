using Paper.Core.UI;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Security.Cryptography;
using System;
using System.Threading;
using Paper.Core;
using System.Text;

namespace Wires.Core.UI;

internal class TruthTable : BorderedElement
{
    public IReadOnlyList<bool[]> TableRows => _rows;

    private readonly List<bool[]> _rows = [];
    private readonly ImmutableArray<string> _headers;

    public TruthTable(Graphics graphics, UIVector2 pos, ImmutableArray<string> headers, IEnumerable<bool[]> rows) : base(pos, default)
    {
        _headers = headers;
        foreach (var row in rows)
            _rows.Add(row);
        UpdateSize(graphics);
    }

    public override bool Update()
    {
        return base.Update();
    }

    public override void Draw()
    {
        base.Draw();
        DrawTable((c, p) =>
        {
            if(c.Y == 0)
            {
                Graphics.DrawStringCentered(_headers[c.X], p);
            }
            else
            {
                var (a, b) = Constants.GetWireColor(_rows[c.Y - 1][c.X] ? Sim.PowerState.OnState : Sim.PowerState.OffState);
                Graphics.ShapeBatch.DrawCircle(p, 6, a, b, 2);
            }
        });
    }

    public void AddRow(bool[] row)
    {
        _rows.Add(row);
        UpdateSize(Graphics);
    }
    float Padding => 8;
    float RowPadding => 8;

    private void UpdateSize(Graphics g)
    {
        int columns = _headers.Length;
        int rows = _rows.Count + 1;
        Vector2 cellSize = new(28);

        SetSize(2 * new Vector2(Padding, Padding) + cellSize * new Vector2(columns, rows));
    }

    private void DrawTable(Action<Point, Vector2> drawCell)
    {
        int columns = _headers.Length;
        int rows = _rows.Count + 1;
        Vector2 startPos = Bounds.Location.ToVector2() + new Vector2(Padding);
        Vector2 cellSize = new(28);

        for (int c = 1; c < columns; c++)
        {
            var x = startPos.X + c * cellSize.X;
            var top = new Vector2(x, startPos.Y);
            var bottom = new Vector2(x, startPos.Y + rows * cellSize.Y);

            Graphics.ShapeBatch.FillLine(top, bottom, 1, Color.White, aaSize: 0);
        }

        if (rows > 1)
        {
            var y = startPos.Y + cellSize.Y;
            var left = new Vector2(startPos.X, y);
            var right = new Vector2(startPos.X + columns * cellSize.X, y);

            Graphics.ShapeBatch.FillLine(left, right, 1, Color.White, aaSize: 0);
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var cellPos = new Vector2(
                    startPos.X + c * cellSize.X + cellSize.X / 2f,
                    startPos.Y + r * cellSize.Y + cellSize.Y / 2f
                );

                drawCell?.Invoke(new Point(c, r), cellPos);
            }
        }
    }

}
