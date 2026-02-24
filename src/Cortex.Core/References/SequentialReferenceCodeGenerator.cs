namespace Cortex.Core.References;

/// <summary>
/// Generates sequential reference codes with daily reset and persistent sequence tracking.
/// Thread-safe via <see cref="SemaphoreSlim"/>. Delegates persistence to <see cref="ISequenceStore"/>.
/// </summary>
public sealed class SequentialReferenceCodeGenerator : IReferenceCodeGenerator, IDisposable
{
    private readonly ISequenceStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Creates a new <see cref="SequentialReferenceCodeGenerator"/>.
    /// </summary>
    /// <param name="store">The sequence store for persistence.</param>
    /// <param name="timeProvider">The time provider for determining the current date.</param>
    public SequentialReferenceCodeGenerator(ISequenceStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<ReferenceCode> GenerateAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            var state = await _store.LoadAsync(cancellationToken);

            var sequence = state.Date == today ? state.Sequence + 1 : 1;

            var newState = new SequenceState(today, sequence);
            await _store.SaveAsync(newState, cancellationToken);

            return ReferenceCode.Create(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), sequence);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
