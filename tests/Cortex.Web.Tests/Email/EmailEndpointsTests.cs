using Cortex.Core.Email;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Web.Email;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Cortex.Web.Tests.Email;

public class EmailWebhookHandlerTests
{
    private readonly IEmailProvider _emailProvider = Substitute.For<IEmailProvider>();
    private readonly InMemoryEmailDeduplicationStore _deduplicationStore = new();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly IReferenceCodeGenerator _referenceCodeGenerator = Substitute.For<IReferenceCodeGenerator>();

    private EmailWebhookHandler CreateHandler()
    {
        _referenceCodeGenerator.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns(ReferenceCode.Create(DateTimeOffset.UtcNow, 1));

        return new EmailWebhookHandler(
            _emailProvider,
            _deduplicationStore,
            _messagePublisher,
            _referenceCodeGenerator,
            NullLogger<EmailWebhookHandler>.Instance);
    }

    [Fact]
    public async Task HandleWebhookAsync_ValidationRequest_ReturnsValidationToken()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns("test-token");

        var handler = CreateHandler();
        var (isValidation, validationToken, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.True(isValidation);
        Assert.Equal("test-token", validationToken);
    }

    [Fact]
    public async Task HandleWebhookAsync_NewEmail_PublishesToCosQueue()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi there",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email]);

        var handler = CreateHandler();
        var (isValidation, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.False(isValidation);
        Assert.Equal(1, count);

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<MessageEnvelope>(e => e.Message is EmailMessage),
            Arg.Is("agent.cos"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_DuplicateEmail_SkipsIt()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi there",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email]);

        await _deduplicationStore.MarkSeenAsync("msg-001");

        var handler = CreateHandler();
        var (_, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.Equal(0, count);

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<MessageEnvelope>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_MultipleEmails_PublishesEach()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email1 = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "First",
            Body = "First email",
            ReceivedAt = DateTimeOffset.UtcNow
        };
        var email2 = new EmailMessage
        {
            ExternalId = "msg-002",
            From = "carol@example.com",
            To = ["bob@example.com"],
            Subject = "Second",
            Body = "Second email",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email1, email2]);

        var handler = CreateHandler();
        var (_, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.Equal(2, count);

        await _messagePublisher.Received(2).PublishAsync(
            Arg.Any<MessageEnvelope>(),
            Arg.Is("agent.cos"),
            Arg.Any<CancellationToken>());
    }
}
