using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Messaging.RabbitMQ.Tests;

public sealed class MessageSerializerTests
{
    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public void Serialize_ProducesValidJsonBody()
    {
        var envelope = CreateEnvelope("hello");

        var (body, messageType) = MessageSerializer.Serialize(envelope);

        Assert.NotNull(body);
        Assert.True(body.Length > 0);
    }

    [Fact]
    public void Serialize_ReturnsAssemblyQualifiedTypeName()
    {
        var envelope = CreateEnvelope("hello");

        var (_, messageType) = MessageSerializer.Serialize(envelope);

        Assert.Contains("TestMessage", messageType);
        Assert.Contains(",", messageType); // Assembly-qualified
    }

    [Fact]
    public void RoundTrip_PreservesMessageContent()
    {
        var original = CreateEnvelope("round-trip");

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.NotNull(deserialized);
        var message = Assert.IsType<TestMessage>(deserialized.Message);
        Assert.Equal("round-trip", message.Content);
    }

    [Fact]
    public void RoundTrip_PreservesReferenceCode()
    {
        var original = CreateEnvelope("ref-test");

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal(original.ReferenceCode, deserialized!.ReferenceCode);
    }

    [Fact]
    public void RoundTrip_PreservesPriority()
    {
        var original = CreateEnvelope("priority") with { Priority = MessagePriority.Critical };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal(MessagePriority.Critical, deserialized!.Priority);
    }

    [Fact]
    public void RoundTrip_PreservesContext()
    {
        var original = CreateEnvelope("context") with
        {
            Context = new MessageContext
            {
                OriginalGoal = "test goal",
                TeamId = "team-1",
                ChannelId = "channel-1"
            }
        };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal("test goal", deserialized!.Context.OriginalGoal);
        Assert.Equal("team-1", deserialized.Context.TeamId);
        Assert.Equal("channel-1", deserialized.Context.ChannelId);
    }

    [Fact]
    public void Deserialize_InvalidType_ReturnsNull()
    {
        var (body, _) = MessageSerializer.Serialize(CreateEnvelope("bad-type"));

        var result = MessageSerializer.Deserialize(body, "NonExistent.Type, NoAssembly");

        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        var badBody = System.Text.Encoding.UTF8.GetBytes("{ not valid json }}}");

        var result = MessageSerializer.Deserialize(
            badBody,
            typeof(TestMessage).AssemblyQualifiedName!);

        Assert.Null(result);
    }

    [Fact]
    public void Serialize_NullEnvelope_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MessageSerializer.Serialize(null!));
    }

    [Fact]
    public void RoundTrip_PreservesSingleAuthorityClaim()
    {
        var original = CreateEnvelope("auth-single") with
        {
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = "user-1",
                    GrantedTo = "agent-1",
                    Tier = AuthorityTier.DoItAndShowMe,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.AuthorityClaims);
        var claim = deserialized.AuthorityClaims[0];
        Assert.Equal("user-1", claim.GrantedBy);
        Assert.Equal("agent-1", claim.GrantedTo);
        Assert.Equal(AuthorityTier.DoItAndShowMe, claim.Tier);
    }

    [Fact]
    public void RoundTrip_PreservesEmptyAuthorityClaims()
    {
        var original = CreateEnvelope("auth-empty");

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.AuthorityClaims);
    }

    [Fact]
    public void RoundTrip_PreservesMultipleAuthorityClaimsWithExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddHours(4);
        var original = CreateEnvelope("auth-multi") with
        {
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = "owner",
                    GrantedTo = "cos-agent",
                    Tier = AuthorityTier.AskMeFirst,
                    PermittedActions = ["send-email", "publish"],
                    GrantedAt = now,
                    ExpiresAt = expiry
                },
                new AuthorityClaim
                {
                    GrantedBy = "cos-agent",
                    GrantedTo = "worker-1",
                    Tier = AuthorityTier.JustDoIt,
                    GrantedAt = now
                }
            ]
        };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.AuthorityClaims.Count);

        var first = deserialized.AuthorityClaims[0];
        Assert.Equal("owner", first.GrantedBy);
        Assert.Equal("cos-agent", first.GrantedTo);
        Assert.Equal(AuthorityTier.AskMeFirst, first.Tier);
        Assert.Equal(["send-email", "publish"], first.PermittedActions);
        Assert.Equal(expiry, first.ExpiresAt);

        var second = deserialized.AuthorityClaims[1];
        Assert.Equal("cos-agent", second.GrantedBy);
        Assert.Equal("worker-1", second.GrantedTo);
        Assert.Equal(AuthorityTier.JustDoIt, second.Tier);
        Assert.Null(second.ExpiresAt);
    }
}
