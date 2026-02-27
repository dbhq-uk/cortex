using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Agents.Tests;

public sealed class InMemoryPendingPlanStoreTests
{
    private readonly InMemoryPendingPlanStore _store = new();

    private static PendingPlan CreatePlan()
    {
        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "test" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        var decomposition = new DecompositionResult
        {
            Summary = "Test plan",
            Confidence = 0.9,
            Tasks =
            [
                new DecompositionTask
                {
                    Capability = "email",
                    Description = "Send report",
                    AuthorityTier = "AskMeFirst"
                }
            ]
        };

        return new PendingPlan
        {
            OriginalEnvelope = envelope,
            Decomposition = decomposition,
            StoredAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsPlan()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var plan = CreatePlan();

        await _store.StoreAsync(refCode, plan);
        var retrieved = await _store.GetAsync(refCode);

        Assert.NotNull(retrieved);
        Assert.Equal(plan.Decomposition.Summary, retrieved.Decomposition.Summary);
        Assert.Equal(plan.StoredAt, retrieved.StoredAt);
    }

    [Fact]
    public async Task GetAsync_Nonexistent_ReturnsNull()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 99);

        var result = await _store.GetAsync(refCode);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var plan = CreatePlan();

        await _store.StoreAsync(refCode, plan);
        await _store.RemoveAsync(refCode);
        var result = await _store.GetAsync(refCode);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 999);

        var exception = await Record.ExceptionAsync(
            () => _store.RemoveAsync(refCode));

        Assert.Null(exception);
    }
}
