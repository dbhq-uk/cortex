using Cortex.Core.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Web.Email;

/// <summary>
/// Background service that periodically checks for expiring webhook subscriptions
/// and renews them before they expire.
/// </summary>
public sealed class SubscriptionRenewalService : IHostedService, IDisposable
{
    private readonly IEmailProvider _emailProvider;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly ILogger<SubscriptionRenewalService> _logger;
    private readonly SubscriptionRenewalOptions _options;

    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new <see cref="SubscriptionRenewalService"/>.
    /// </summary>
    /// <param name="emailProvider">Email provider for renewing subscriptions.</param>
    /// <param name="subscriptionStore">Store for querying and updating subscription records.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Renewal configuration.</param>
    public SubscriptionRenewalService(
        IEmailProvider emailProvider,
        ISubscriptionStore subscriptionStore,
        ILogger<SubscriptionRenewalService> logger,
        SubscriptionRenewalOptions options)
    {
        ArgumentNullException.ThrowIfNull(emailProvider);
        ArgumentNullException.ThrowIfNull(subscriptionStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _emailProvider = emailProvider;
        _subscriptionStore = subscriptionStore;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Checks for expiring subscriptions and renews them.
    /// Public for testing.
    /// </summary>
    public async Task CheckAndRenewAsync(CancellationToken cancellationToken = default)
    {
        var expiring = await _subscriptionStore.GetExpiringAsync(_options.RenewalWindow, cancellationToken);
        if (expiring.Count == 0)
        {
            return;
        }

        foreach (var record in expiring)
        {
            try
            {
                var renewed = await _emailProvider.RenewSubscriptionAsync(record.SubscriptionId, cancellationToken);
                await _subscriptionStore.UpdateExpiryAsync(record.SubscriptionId, renewed.ExpiresAt, cancellationToken);

                _logger.LogInformation(
                    "Renewed subscription {SubscriptionId} for user {UserId}, new expiry {ExpiresAt}",
                    record.SubscriptionId, record.UserId, renewed.ExpiresAt);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to renew subscription {SubscriptionId} for user {UserId}",
                    record.SubscriptionId, record.UserId);
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription renewal service starting with check interval {Interval}",
            _options.CheckInterval);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_options.CheckInterval);
        _loopTask = RunLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription renewal service stopping");

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _logger.LogInformation("Subscription renewal service stopped");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (await _timer!.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await CheckAndRenewAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during subscription renewal check");
            }
        }
    }
}
