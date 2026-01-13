using Frent;

namespace Wires.Core;

public abstract record class TickResult
{
    public sealed record class ShortCircuit(Entity Wire, Entity ComponentA, Entity ComponentB) : TickResult;
    public sealed record class Timeout : TickResult;
    public sealed record class InnerError(Entity Component, TickResult Error) : TickResult;
    public sealed record class NoError : TickResult;
    public static readonly TickResult Success = new NoError();
}
