using Cortex.Core.Messages;

namespace Cortex.Core.Tests.Messages;

public class EmailMessageTests
{
    [Fact]
    public void EmailAttachment_CarriesMetadata()
    {
        var attachment = new EmailAttachment
        {
            FileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            ContentId = "att-001"
        };

        Assert.Equal("report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1024, attachment.SizeBytes);
        Assert.Equal("att-001", attachment.ContentId);
    }

    [Fact]
    public void EmailMessage_ImplementsIMessage_HasMessageIdAndTimestamp()
    {
        var email = new EmailMessage
        {
            ExternalId = "graph-msg-123",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi Bob",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        IMessage message = email;
        Assert.NotNull(message.MessageId);
        Assert.NotEqual(default, message.Timestamp);
    }

    [Fact]
    public void EmailMessage_CarriesAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var attachment = new EmailAttachment
        {
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            ContentId = "att-002"
        };

        var email = new EmailMessage
        {
            ExternalId = "graph-msg-456",
            From = "alice@example.com",
            To = ["bob@example.com", "carol@example.com"],
            Subject = "Project update",
            Body = "Here is the update",
            ThreadId = "thread-789",
            Cc = ["dave@example.com"],
            Attachments = [attachment],
            ReceivedAt = now
        };

        Assert.Equal("graph-msg-456", email.ExternalId);
        Assert.Equal("alice@example.com", email.From);
        Assert.Equal(2, email.To.Count);
        Assert.Equal("Project update", email.Subject);
        Assert.Equal("Here is the update", email.Body);
        Assert.Equal("thread-789", email.ThreadId);
        Assert.Single(email.Cc);
        Assert.Single(email.Attachments);
        Assert.Equal(now, email.ReceivedAt);
    }

    [Fact]
    public void EmailMessage_DefaultCollections_AreEmpty()
    {
        var email = new EmailMessage
        {
            ExternalId = "graph-msg-789",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Test",
            Body = "Test body",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        Assert.Empty(email.Cc);
        Assert.Empty(email.Attachments);
        Assert.Null(email.ThreadId);
        Assert.Null(email.CorrelationId);
    }

    [Fact]
    public void OutboundEmail_CarriesAllFields()
    {
        var outbound = new OutboundEmail
        {
            To = "bob@example.com",
            Subject = "Re: Hello",
            Body = "Thanks for your email",
            Cc = ["carol@example.com"],
            InReplyToExternalId = "graph-msg-123"
        };

        Assert.Equal("bob@example.com", outbound.To);
        Assert.Equal("Re: Hello", outbound.Subject);
        Assert.Equal("Thanks for your email", outbound.Body);
        Assert.Single(outbound.Cc);
        Assert.Equal("graph-msg-123", outbound.InReplyToExternalId);
    }

    [Fact]
    public void OutboundEmail_DefaultCollections_AreEmpty()
    {
        var outbound = new OutboundEmail
        {
            To = "bob@example.com",
            Subject = "Test",
            Body = "Test body"
        };

        Assert.Empty(outbound.Cc);
        Assert.Empty(outbound.Attachments);
        Assert.Null(outbound.InReplyToExternalId);
    }
}
