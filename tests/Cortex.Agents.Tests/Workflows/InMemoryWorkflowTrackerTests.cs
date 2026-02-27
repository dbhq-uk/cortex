using Cortex.Agents.Workflows;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Agents.Tests.Workflows;

public sealed class InMemoryWorkflowTrackerTests
{
    private readonly InMemoryWorkflowTracker _tracker = new();

    private static MessageEnvelope CreateEnvelope(string content = "test") => new()
    {
        Message = new TestMessage { Content = content },
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
        Context = new MessageContext { ReplyTo = "agent.requester" }
    };

    private static WorkflowRecord CreateWorkflow(
        ReferenceCode? parentRef = null,
        IReadOnlyList<ReferenceCode>? subtaskRefs = null)
    {
        var parent = parentRef ?? ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var children = subtaskRefs ?? [
            ReferenceCode.Create(DateTimeOffset.UtcNow, 2),
            ReferenceCode.Create(DateTimeOffset.UtcNow, 3)
        ];

        return new WorkflowRecord
        {
            ReferenceCode = parent,
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = children,
            Summary = "Test workflow"
        };
    }

    // --- CreateAsync and GetAsync ---

    [Fact]
    public async Task CreateAsync_ThenGetAsync_ReturnsWorkflow()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        var retrieved = await _tracker.GetAsync(workflow.ReferenceCode);

        Assert.NotNull(retrieved);
        Assert.Equal(workflow.ReferenceCode, retrieved.ReferenceCode);
        Assert.Equal("Test workflow", retrieved.Summary);
    }

    [Fact]
    public async Task GetAsync_UnknownRefCode_ReturnsNull()
    {
        var result = await _tracker.GetAsync(ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Null(result);
    }

    // --- FindBySubtaskAsync ---

    [Fact]
    public async Task FindBySubtaskAsync_KnownSubtask_ReturnsWorkflow()
    {
        var childRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var workflow = CreateWorkflow(subtaskRefs: [childRef]);
        await _tracker.CreateAsync(workflow);

        var found = await _tracker.FindBySubtaskAsync(childRef);

        Assert.NotNull(found);
        Assert.Equal(workflow.ReferenceCode, found.ReferenceCode);
    }

    [Fact]
    public async Task FindBySubtaskAsync_UnknownRefCode_ReturnsNull()
    {
        var result = await _tracker.FindBySubtaskAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBySubtaskAsync_ParentRefCode_ReturnsNull()
    {
        var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var workflow = CreateWorkflow(parentRef: parentRef);
        await _tracker.CreateAsync(workflow);

        var result = await _tracker.FindBySubtaskAsync(parentRef);

        Assert.Null(result);
    }

    // --- UpdateStatusAsync ---

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        await _tracker.UpdateStatusAsync(workflow.ReferenceCode, WorkflowStatus.Completed);

        var retrieved = await _tracker.GetAsync(workflow.ReferenceCode);
        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowStatus.Completed, retrieved.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownRefCode_NoOp()
    {
        // Should not throw
        await _tracker.UpdateStatusAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999),
            WorkflowStatus.Failed);
    }

    // --- StoreSubtaskResultAsync and GetCompletedResultsAsync ---

    [Fact]
    public async Task StoreSubtaskResultAsync_ThenGetCompletedResults_ReturnsResult()
    {
        var childRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var workflow = CreateWorkflow(subtaskRefs: [childRef]);
        await _tracker.CreateAsync(workflow);

        var resultEnvelope = CreateEnvelope("sub-task result");
        await _tracker.StoreSubtaskResultAsync(childRef, resultEnvelope);

        var results = await _tracker.GetCompletedResultsAsync(workflow.ReferenceCode);
        Assert.Single(results);
        Assert.True(results.ContainsKey(childRef));
    }

    [Fact]
    public async Task GetCompletedResultsAsync_NoResults_ReturnsEmpty()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        var results = await _tracker.GetCompletedResultsAsync(workflow.ReferenceCode);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetCompletedResultsAsync_UnknownWorkflow_ReturnsEmpty()
    {
        var results = await _tracker.GetCompletedResultsAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Empty(results);
    }

    // --- AllSubtasksCompleteAsync ---

    [Fact]
    public async Task AllSubtasksCompleteAsync_NoneComplete_ReturnsFalse()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        Assert.False(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_PartialComplete_ReturnsFalse()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        await _tracker.StoreSubtaskResultAsync(child1, CreateEnvelope("result 1"));

        Assert.False(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_AllComplete_ReturnsTrue()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        await _tracker.StoreSubtaskResultAsync(child1, CreateEnvelope("result 1"));
        await _tracker.StoreSubtaskResultAsync(child2, CreateEnvelope("result 2"));

        Assert.True(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_UnknownWorkflow_ReturnsFalse()
    {
        Assert.False(await _tracker.AllSubtasksCompleteAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999)));
    }
}
