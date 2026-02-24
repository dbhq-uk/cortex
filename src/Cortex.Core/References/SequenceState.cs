namespace Cortex.Core.References;

/// <summary>
/// The persisted state of a reference code sequence — the date and current sequence number.
/// </summary>
/// <param name="Date">The date this sequence belongs to.</param>
/// <param name="Sequence">The last sequence number generated for this date.</param>
public record SequenceState(DateOnly Date, int Sequence);
