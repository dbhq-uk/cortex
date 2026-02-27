using Cortex.Core.Email;
using Cortex.Web.Email;

var builder = WebApplication.CreateBuilder(args);

// Email provider configuration
builder.Services.Configure<EmailProviderOptions>(
    builder.Configuration.GetSection("Email"));

// Email services
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<IEmailDeduplicationStore, InMemoryEmailDeduplicationStore>();
builder.Services.AddSingleton<IAttachmentStore, InMemoryAttachmentStore>();
builder.Services.AddSingleton<ISubscriptionStore, InMemorySubscriptionStore>();
builder.Services.AddSingleton<IEmailProvider, MicrosoftGraphEmailProvider>();
builder.Services.AddSingleton<EmailWebhookHandler>();

// Subscription renewal background service
builder.Services.AddSingleton(new SubscriptionRenewalOptions());
builder.Services.AddHostedService<SubscriptionRenewalService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapEmailEndpoints();

app.Run();
