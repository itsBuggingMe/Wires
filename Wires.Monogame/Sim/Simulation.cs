using System;
using System.Runtime.CompilerServices;
using Wires.Core;

namespace Wires.Sim;

public class Simulation
{
    private Tile[] _tiles;
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Span<Wire> Wires => _wires.AsSpan();

    private FastStack<Wire> _wires = new(16);
    private int _wireFreeHead = -1;

    public ref Tile this[int x, int y]
    {
        get => ref _tiles[x + y * Width];
    }

    public Simulation(int width = 100, int height = 100)
    {
        _tiles = new Tile[width * height];
        Width = width;
        Height = height;
    }

    public void Step()
    {

    }

    public void DestroyWire(int wireId)
    {
        ref Wire toDestroy = ref _wires[wireId];
        toDestroy.From.X = _wireFreeHead;
        toDestroy.Exists = false;
        _wireFreeHead = wireId;
    }

    public ref Wire CreateWire(out int wireId)
    {
        ref Wire next = ref Unsafe.NullRef<Wire>();

        if(_wireFreeHead == -1)
        {
            wireId = _wires.Count;
            next = ref _wires.PushRef();
        }
        else
        {
            wireId = _wireFreeHead;
            next = ref _wires[_wireFreeHead];
            _wireFreeHead = next.From.X;
        }

        next.Exists = true;
        return ref next;
    }
}