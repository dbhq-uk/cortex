using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDelegationTracker"/>.
/// </summary>
public sealed class InMemoryDelegationTracker : IDelegationTracker
{
    private readonly ConcurrentDictionary<string, DelegationRecord> _records = new();

    /// <inheritdoc />
    public Task DelegateAsync(DelegationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.ReferenceCode.Value] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(ReferenceCode referenceCode, DelegationStatus status, CancellationToken cancellationToken = default)
    {
        if (_records.TryGetValue(referenceCode.Value, out var existing))
        {
            _records[referenceCode.Value] = existing with { Status = status };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var matches = _records.Values
            .Where(r => string.Equals(r.DelegatedTo, agentId, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<DelegationRecord>>(matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var overdue = _records.Values
            .Where(r => r.DueAt.HasValue
                && r.DueAt.Value < now
                && r.Status != DelegationStatus.Complete)
            .ToList();

        return Task.FromResult<IReadOnlyList<DelegationRecord>>(overdue);
    }
}
