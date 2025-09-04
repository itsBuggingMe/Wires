using System;

namespace Wires.Core;

public struct FreeStack<T> where T : IFreeListId
{
    private FastStack<T> _items;
    private int _freeHead;

    public FreeStack(int capacity = 16)
    {
        _items = new FastStack<T>(capacity);
        _freeHead = -1;
    }

    public int Count => _items.Count;

    public ref T this[int index] => ref _items[index];

    public Span<T> AsSpan() => _items.AsSpan();

    public ref T Create(out int id)
    {
        if (_freeHead == -1)
        {
            id = _items.Count;
            ref T item = ref _items.PushRef();
            item.Exists = true;
            return ref item;
        }
        else
        {
            id = _freeHead;
            ref T item = ref _items[_freeHead];

            _freeHead = item.FreeNext;
            item.Exists = true;
            return ref item;
        }
    }

    public void Destroy(int id)
    {
        ref T item = ref _items[id];
        item.Exists = false;
        item.FreeNext = _freeHead;
        _freeHead = id;
    }
}

public interface IFreeListId
{
    int FreeNext { get; set; }
    bool Exists { set; }
}