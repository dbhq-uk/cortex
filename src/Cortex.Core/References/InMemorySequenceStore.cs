namespace Cortex.Core.References;

/// <summary>
/// In-memory implementation of <see cref="ISequenceStore"/> for testing and local development.
/// State is lost when the process exits.
/// </summary>
public sealed class InMemorySequenceStore : ISequenceStore
{
    private SequenceState _state = new(DateOnly.MinValue, 0);

    /// <inheritdoc />
    public Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    /// <inheritdoc />
    public Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        return Task.CompletedTask;
    }
}
