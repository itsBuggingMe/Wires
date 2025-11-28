using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wires.Core.Sim;

public class GlobalStateTable
{
    public PowerState this[ulong address]
    {
        get => _addressMapRead.GetValueOrDefault(address);
        set => _addressMapWrite[address] = value;
    }

    private Dictionary<ulong, PowerState> _addressMapWrite = [];
    private Dictionary<ulong, PowerState> _addressMapRead = [];


    public static ulong CreateAddress(ulong previousAddress, Point pos)
    {
        int x = pos.X;
        int y = pos.Y;

        int newHash1 = (previousAddress, x, y).GetHashCode();
        int newHash2 = (x, y, previousAddress).GetHashCode();

        ulong newAddress = ((ulong)(uint)newHash1 << 32) | (uint)newHash2;
        return newAddress;
    }

    public void SwapBuffers()
    {
        (_addressMapRead, _addressMapWrite) = (_addressMapWrite, _addressMapRead);
    }

    public void Reset()
    {
        _addressMapWrite.Clear();
        _addressMapRead.Clear();
    }
}
