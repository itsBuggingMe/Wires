using System.Numerics;
using System;

namespace Wires.Core;

internal struct FastStack<T>(int capacity)
{
    private T[] _buffer = new T[capacity];
    private int _nextIndex;

    public readonly int Count => _nextIndex;
    public readonly ref T this[int index] => ref _buffer[index];

    public ref T PushRef() => ref MemoryHelper.GetValueOrResize(ref _buffer, _nextIndex++);
    public T Pop() => _buffer[--_nextIndex];
    public Span<T> AsSpan() => _buffer.AsSpan(0, _nextIndex);
}

internal static class MemoryHelper
{
    public static ref T GetValueOrResize<T>(scoped ref T[] arr, int index)
    {
        var arrLoc = arr;
        if ((uint)index < (uint)arrLoc.Length)
            return ref arrLoc[index];
        return ref ResizeAndGet(ref arr, index);
    }

    private static ref T ResizeAndGet<T>(scoped ref T[] arr, int index)
    {
        int newSize = (int)BitOperations.RoundUpToPowerOf2((uint)(index + 1));
        Array.Resize(ref arr, newSize);
        return ref arr[index];
    }
}