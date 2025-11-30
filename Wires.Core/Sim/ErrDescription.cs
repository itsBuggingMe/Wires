namespace Wires.Core.Sim;

public sealed record class ErrDescription(int ComponentIdA, int ComponentIdB, PowerState A, PowerState B, int WireId, ErrDescription? Next = null, bool IsCircularDep = false);