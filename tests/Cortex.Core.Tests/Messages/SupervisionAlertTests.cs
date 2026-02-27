using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Messages;

public sealed class SupervisionAlertTests
{
    [Fact]
    public void SupervisionAlert_ImplementsIMessage_CarriesAllFields()
    {
        var before = DateTimeOffset.UtcNow;
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var dueAt = DateTimeOffset.UtcNow.AddHours(-1);

        var alert = new SupervisionAlert
        {
            DelegationReferenceCode = refCode,
            DelegatedTo = "agent-1",
            Description = "Overdue task",
            RetryCount = 2,
            DueAt = dueAt,
            IsAgentRunning = true
        };

        var after = DateTimeOffset.UtcNow;

        IMessage message = alert;
        Assert.NotNull(message.MessageId);
        Assert.NotEmpty(message.MessageId);
        Assert.InRange(message.Timestamp, before, after);
        Assert.Equal(refCode, alert.DelegationReferenceCode);
        Assert.Equal("agent-1", alert.DelegatedTo);
        Assert.Equal("Overdue task", alert.Description);
        Assert.Equal(2, alert.RetryCount);
        Assert.Equal(dueAt, alert.DueAt);
        Assert.True(alert.IsAgentRunning);
    }

    [Fact]
    public void EscalationAlert_ImplementsIMessage_CarriesReason()
    {
        var before = DateTimeOffset.UtcNow;
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);

        var alert = new EscalationAlert
        {
            DelegationReferenceCode = refCode,
            DelegatedTo = "agent-2",
            Description = "Escalated task",
            RetryCount = 5,
            Reason = "Max retries exceeded"
        };

        var after = DateTimeOffset.UtcNow;

        IMessage message = alert;
        Assert.NotNull(message.MessageId);
        Assert.NotEmpty(message.MessageId);
        Assert.InRange(message.Timestamp, before, after);
        Assert.Equal(refCode, alert.DelegationReferenceCode);
        Assert.Equal("agent-2", alert.DelegatedTo);
        Assert.Equal("Escalated task", alert.Description);
        Assert.Equal(5, alert.RetryCount);
        Assert.Equal("Max retries exceeded", alert.Reason);
    }

    [Fact]
    public void SupervisionAlert_DeadAgentFlag_IsAgentRunningFalse()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);

        var alert = new SupervisionAlert
        {
            DelegationReferenceCode = refCode,
            DelegatedTo = "dead-agent",
            Description = "Agent not responding",
            RetryCount = 1,
            DueAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            IsAgentRunning = false
        };

        Assert.False(alert.IsAgentRunning);
        Assert.Equal("dead-agent", alert.DelegatedTo);
    }
}
