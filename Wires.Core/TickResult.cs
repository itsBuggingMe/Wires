using Frent;

namespace Wires.Core;

public abstract record class TickResult
{
    public abstract record class Error : TickResult;

    public sealed record class ShortCircuit(Entity Wire, Entity ComponentA, Entity ComponentB) : Error;
    public sealed record class Timeout : Error;
    public sealed record class InnerError(Entity Component, Error InnerResult) : Error;

    public sealed record class Success : TickResult;

    public static readonly Success SuccessInstance = new();
}
