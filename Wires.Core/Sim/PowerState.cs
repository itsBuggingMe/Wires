using System.Diagnostics;

namespace Wires.Core.Sim;

[DebuggerDisplay("{On ? \"On\" : \"Off\",nq}")]
public record struct PowerState(byte Values)
{
    public readonly bool On => Values != 0;
    public readonly bool Off => Values == 0;
    public readonly bool OnAt(int index) => (Values & (1 << index)) != 0;

    public static readonly PowerState OnState = new PowerState(1);
    public static readonly PowerState OffState = new PowerState(0);
}