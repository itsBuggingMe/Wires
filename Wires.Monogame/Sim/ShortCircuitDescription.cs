namespace Wires.Sim;

public record class ShortCircuitDescription(int ComponentIdA, int ComponentIdB, PowerState A, PowerState B, int WireId, ShortCircuitDescription? Next = null);