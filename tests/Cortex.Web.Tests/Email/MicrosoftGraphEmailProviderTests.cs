using Cortex.Core.Email;
using Cortex.Core.Messages;
using Cortex.Web.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.Web.Tests.Email;

public class MicrosoftGraphEmailProviderTests
{
    [Fact]
    public void HandleValidation_WithValidationToken_ReturnsToken()
    {
        var options = Options.Create(new EmailProviderOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback"
        });

        var tokenStore = new InMemoryTokenStore();
        var provider = new MicrosoftGraphEmailProvider(options, tokenStore, NullLogger<MicrosoftGraphEmailProvider>.Instance);

        var headers = new Dictionary<string, string>();
        var payload = "{\"validationToken\": \"test-token-123\"}";

        var result = provider.HandleValidation(payload, headers);

        Assert.Equal("test-token-123", result);
    }

    [Fact]
    public void HandleValidation_WithoutValidationToken_ReturnsNull()
    {
        var options = Options.Create(new EmailProviderOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback"
        });

        var tokenStore = new InMemoryTokenStore();
        var provider = new MicrosoftGraphEmailProvider(options, tokenStore, NullLogger<MicrosoftGraphEmailProvider>.Instance);

        var headers = new Dictionary<string, string>();
        var payload = "{\"value\": []}";

        var result = provider.HandleValidation(payload, headers);

        Assert.Null(result);
    }
}
