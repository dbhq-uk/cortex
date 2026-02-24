namespace Cortex.Core.References;

/// <summary>
/// Persists reference code sequence state across restarts.
/// </summary>
public interface ISequenceStore
{
    /// <summary>
    /// Loads the current sequence state, or returns a zeroed state if none exists.
    /// </summary>
    Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current sequence state.
    /// </summary>
    Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default);
}
