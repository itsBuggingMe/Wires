using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
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
    private const int TotalMemory = ushort.MaxValue;
    private readonly byte[] _readMemory = new byte[TotalMemory];
    private readonly byte[] _writeMemory = new byte[TotalMemory];
    private readonly Dictionary<ulong, (ushort Address, PowerState Value)> _writtenAddresses = [];

    public GlobalStateTable()
    {
        for(int i = 0; i < TotalMemory; i++)
        {
            _readMemory[i] = _writeMemory[i] = (byte)Random.Shared.Next();
        }

        int j = 0;

        WriteSeq(0b0000010);
        WriteSeq(0b01110110);   // HLT

        WriteSeq(0b00_000_110); // MOV B, 69
        WriteSeq(69);
        WriteSeq(0b00_111_110); // MOV A, 42
        WriteSeq(42);
        WriteSeq(0b10_000_000); // XRA A
        WriteSeq(0b00100001);   // LXI HL, 500
        WriteSeq(0b11110100);
        WriteSeq(0b00000001);
        WriteSeq(0b00111010);


        WriteSeq(0b01110110);   // HLT

        void WriteSeq(byte b)
        {
            _readMemory[j] = _writeMemory[j] = b;
            j++;
        }
    }

    public static ulong CreateAddress(ulong previousAddress, Point pos)
    {
        int x = pos.X;
        int y = pos.Y;

        int newHash1 = (previousAddress, x, y).GetHashCode();
        int newHash2 = (x, y, previousAddress).GetHashCode();

        ulong newAddress = ((ulong)(uint)newHash1 << 32) | (uint)newHash2;
        return newAddress;
    }

    public PowerState TickRam(ulong componentHash, PowerState enableWrite, PowerState writeValue, ushort address)
    {
        if(enableWrite.On)
        {
            _writtenAddresses[componentHash] = (address, writeValue);
        }
        else
        {
            _writtenAddresses.Remove(componentHash);
        }
        return new(_readMemory[address]);
    }

    public void SwapBuffers()
    {
        (_addressMapRead, _addressMapWrite) = (_addressMapWrite, _addressMapRead);

        foreach(var kvp in _writtenAddresses)
        {
            _readMemory[kvp.Value.Address] = _writeMemory[kvp.Value.Address] = kvp.Value.Value.Values;
        }

        _writtenAddresses.Clear();
    }

    public void Reset()
    {
        _addressMapWrite.Clear();
        _addressMapRead.Clear();
    }
}
