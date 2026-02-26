using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Core.Tests.Workflows;

public sealed class WorkflowRecordTests
{
    private static MessageEnvelope CreateEnvelope() => new()
    {
        Message = new TextMessage("test"),
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
        Context = new MessageContext { ReplyTo = "agent.requester" }
    };

    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var childRef2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var envelope = CreateEnvelope();

        var record = new WorkflowRecord
        {
            ReferenceCode = parentRef,
            OriginalEnvelope = envelope,
            SubtaskReferenceCodes = [childRef1, childRef2],
            Summary = "Quarterly report"
        };

        Assert.Equal(parentRef, record.ReferenceCode);
        Assert.Same(envelope, record.OriginalEnvelope);
        Assert.Equal(2, record.SubtaskReferenceCodes.Count);
        Assert.Equal("Quarterly report", record.Summary);
    }

    [Fact]
    public void Status_DefaultsToInProgress()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        Assert.Equal(WorkflowStatus.InProgress, record.Status);
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(record.CreatedAt, before, after);
    }

    [Fact]
    public void CompletedAt_DefaultsToNull()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        Assert.Null(record.CompletedAt);
    }

    [Fact]
    public void With_CanUpdateStatus()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        var completed = record with
        {
            Status = WorkflowStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
    }
}
