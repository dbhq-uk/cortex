using Cortex.Agents.Pipeline;
using Cortex.Core.Authority;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class TriageResultTests
{
    [Fact]
    public void Construction_WithAllProperties_Succeeds()
    {
        var result = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.DoItAndShowMe,
            Summary = "Draft reply to client email",
            Confidence = 0.92
        };

        Assert.Equal("email-drafting", result.Capability);
        Assert.Equal(AuthorityTier.DoItAndShowMe, result.AuthorityTier);
        Assert.Equal("Draft reply to client email", result.Summary);
        Assert.Equal(0.92, result.Confidence);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.JustDoIt,
            Summary = "Draft reply",
            Confidence = 0.85
        };

        var b = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.JustDoIt,
            Summary = "Draft reply",
            Confidence = 0.85
        };

        Assert.Equal(a, b);
    }
}
