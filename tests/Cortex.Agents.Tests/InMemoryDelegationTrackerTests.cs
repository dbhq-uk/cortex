using Cortex.Agents.Delegation;
using Cortex.Core.References;

namespace Cortex.Agents.Tests;

public sealed class InMemoryDelegationTrackerTests
{
    private readonly InMemoryDelegationTracker _tracker = new();
    private int _sequenceCounter;

    private DelegationRecord CreateRecord(
        string delegatedTo = "agent-1",
        DelegationStatus status = DelegationStatus.Assigned,
        DateTimeOffset? dueAt = null) =>
        new()
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, Interlocked.Increment(ref _sequenceCounter)),
            DelegatedBy = "cos-agent",
            DelegatedTo = delegatedTo,
            Description = "Test task",
            Status = status,
            AssignedAt = DateTimeOffset.UtcNow,
            DueAt = dueAt
        };

    [Fact]
    public async Task DelegateAsync_NullRecord_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _tracker.DelegateAsync(null!));
    }

    [Fact]
    public async Task DelegateAsync_ThenGetByAssignee_ReturnsRecord()
    {
        var record = CreateRecord("agent-1");
        await _tracker.DelegateAsync(record);

        var results = await _tracker.GetByAssigneeAsync("agent-1");

        Assert.Single(results);
        Assert.Equal(record.ReferenceCode, results[0].ReferenceCode);
    }

    [Fact]
    public async Task GetByAssigneeAsync_NoRecords_ReturnsEmpty()
    {
        var results = await _tracker.GetByAssigneeAsync("nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var record = CreateRecord("agent-1");
        await _tracker.DelegateAsync(record);

        await _tracker.UpdateStatusAsync(record.ReferenceCode, DelegationStatus.InProgress);

        var results = await _tracker.GetByAssigneeAsync("agent-1");
        Assert.Equal(DelegationStatus.InProgress, results[0].Status);
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsPastDueRecords()
    {
        var overdue = CreateRecord("agent-1", dueAt: DateTimeOffset.UtcNow.AddHours(-1));
        var notDue = CreateRecord("agent-2", dueAt: DateTimeOffset.UtcNow.AddHours(1));
        var noDueDate = CreateRecord("agent-3");

        await _tracker.DelegateAsync(overdue);
        await _tracker.DelegateAsync(notDue);
        await _tracker.DelegateAsync(noDueDate);

        var results = await _tracker.GetOverdueAsync();

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].DelegatedTo);
    }

    [Fact]
    public async Task GetOverdueAsync_ExcludesCompletedRecords()
    {
        var record = CreateRecord("agent-1", dueAt: DateTimeOffset.UtcNow.AddHours(-1));
        await _tracker.DelegateAsync(record);
        await _tracker.UpdateStatusAsync(record.ReferenceCode, DelegationStatus.Complete);

        var results = await _tracker.GetOverdueAsync();

        Assert.Empty(results);
    }
}
