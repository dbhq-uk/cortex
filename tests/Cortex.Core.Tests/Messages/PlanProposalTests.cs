using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Messages;

public sealed class PlanProposalTests
{
    [Fact]
    public void PlanProposal_ImplementsIMessage_HasMessageIdAndTimestamp()
    {
        var before = DateTimeOffset.UtcNow;

        var proposal = new PlanProposal
        {
            Summary = "Quarterly report plan",
            TaskDescriptions = ["Gather data", "Compile report"],
            OriginalGoal = "Create quarterly report",
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        var after = DateTimeOffset.UtcNow;

        IMessage message = proposal;
        Assert.NotNull(message.MessageId);
        Assert.NotEmpty(message.MessageId);
        Assert.InRange(message.Timestamp, before, after);
    }

    [Fact]
    public void PlanProposal_CarriesTaskDescriptions()
    {
        var descriptions = new List<string> { "Task A", "Task B", "Task C" };

        var proposal = new PlanProposal
        {
            Summary = "Multi-step plan",
            TaskDescriptions = descriptions,
            OriginalGoal = "Accomplish the goal",
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 2)
        };

        Assert.Equal(3, proposal.TaskDescriptions.Count);
        Assert.Equal("Task A", proposal.TaskDescriptions[0]);
        Assert.Equal("Task B", proposal.TaskDescriptions[1]);
        Assert.Equal("Task C", proposal.TaskDescriptions[2]);
        Assert.Equal("Accomplish the goal", proposal.OriginalGoal);
    }

    [Fact]
    public void PlanApprovalResponse_Approved()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);

        var response = new PlanApprovalResponse
        {
            IsApproved = true,
            WorkflowReferenceCode = refCode
        };

        Assert.True(response.IsApproved);
        Assert.Null(response.RejectionReason);
        Assert.Equal(refCode, response.WorkflowReferenceCode);
    }

    [Fact]
    public void PlanApprovalResponse_RejectedWithReason()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 4);

        var response = new PlanApprovalResponse
        {
            IsApproved = false,
            RejectionReason = "Too many tasks",
            WorkflowReferenceCode = refCode
        };

        Assert.False(response.IsApproved);
        Assert.Equal("Too many tasks", response.RejectionReason);
        Assert.Equal(refCode, response.WorkflowReferenceCode);
    }
}
