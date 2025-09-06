using System;
using System.Collections.Generic;

namespace Wires.Sim;
#nullable enable
internal class TestCases
{
    private (PowerState[] Input, PowerState[] Output)[]? _data;
    public readonly int Length;

    public TestCases((PowerState[] Input, PowerState[] Output)[] cases)
    {
        _data = cases;
        Length = cases.Length;
    }

    public void Set(int index, PowerState[] inputs, PowerState[] outputs)
    {
        if(_data is not null)
        {
            _data[index].Input.AsSpan().CopyTo(inputs);
            _data[index].Output.AsSpan().CopyTo(outputs);
            return;
        }

        throw new Exception();
    }
}