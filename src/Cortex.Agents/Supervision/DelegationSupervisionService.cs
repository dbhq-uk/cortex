using Cortex.Agents.Delegation;
using Cortex.Core.Messages;
using Cortex.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents.Supervision;

/// <summary>
/// Background service that periodically checks for overdue delegations and publishes
/// supervision or escalation alerts.
/// </summary>
public sealed class DelegationSupervisionService : IHostedService, IDisposable
{
    private readonly IDelegationTracker _delegationTracker;
    private readonly IRetryCounter _retryCounter;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DelegationSupervisionService> _logger;
    private readonly SupervisionOptions _options;
    private readonly IAgentRuntime? _agentRuntime;

    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new <see cref="DelegationSupervisionService"/>.
    /// </summary>
    /// <param name="delegationTracker">Tracker for querying overdue delegations.</param>
    /// <param name="retryCounter">Counter for tracking retry attempts per delegation.</param>
    /// <param name="messagePublisher">Publisher for sending alerts.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Supervision configuration.</param>
    /// <param name="agentRuntime">Optional runtime for dead agent detection.</param>
    public DelegationSupervisionService(
        IDelegationTracker delegationTracker,
        IRetryCounter retryCounter,
        IMessagePublisher messagePublisher,
        ILogger<DelegationSupervisionService> logger,
        SupervisionOptions options,
        IAgentRuntime? agentRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(delegationTracker);
        ArgumentNullException.ThrowIfNull(retryCounter);
        ArgumentNullException.ThrowIfNull(messagePublisher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _delegationTracker = delegationTracker;
        _retryCounter = retryCounter;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _options = options;
        _agentRuntime = agentRuntime;
    }

    /// <summary>
    /// Checks for overdue delegations and publishes appropriate alerts.
    /// Public for testing.
    /// </summary>
    public async Task CheckOverdueAsync(CancellationToken cancellationToken = default)
    {
        var overdue = await _delegationTracker.GetOverdueAsync(cancellationToken);
        if (overdue.Count == 0)
        {
            return;
        }

        var runningAgentIds = _agentRuntime?.RunningAgentIds ?? [];

        foreach (var record in overdue)
        {
            var retryCount = await _retryCounter.IncrementAsync(record.ReferenceCode, cancellationToken);
            var isAgentRunning = _agentRuntime is null || runningAgentIds.Contains(record.DelegatedTo);

            if (retryCount > _options.MaxRetries)
            {
                var alert = new EscalationAlert
                {
                    DelegationReferenceCode = record.ReferenceCode,
                    DelegatedTo = record.DelegatedTo,
                    Description = record.Description,
                    RetryCount = retryCount,
                    Reason = "Max retries exceeded"
                };

                var envelope = new MessageEnvelope
                {
                    Message = alert,
                    ReferenceCode = record.ReferenceCode,
                    Context = new MessageContext { FromAgentId = "supervision-service" }
                };

                await _messagePublisher.PublishAsync(envelope, _options.EscalationTarget, cancellationToken);

                _logger.LogWarning(
                    "Escalated overdue delegation {ReferenceCode} to {Target} after {RetryCount} retries",
                    record.ReferenceCode, _options.EscalationTarget, retryCount);
            }
            else
            {
                var alert = new SupervisionAlert
                {
                    DelegationReferenceCode = record.ReferenceCode,
                    DelegatedTo = record.DelegatedTo,
                    Description = record.Description,
                    RetryCount = retryCount,
                    DueAt = record.DueAt!.Value,
                    IsAgentRunning = isAgentRunning
                };

                var envelope = new MessageEnvelope
                {
                    Message = alert,
                    ReferenceCode = record.ReferenceCode,
                    Context = new MessageContext { FromAgentId = "supervision-service" }
                };

                await _messagePublisher.PublishAsync(envelope, _options.AlertTarget, cancellationToken);

                _logger.LogInformation(
                    "Published supervision alert for {ReferenceCode} (retry {RetryCount}, agent running: {IsAgentRunning})",
                    record.ReferenceCode, retryCount, isAgentRunning);
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Delegation supervision service starting with check interval {Interval}",
            _options.CheckInterval);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_options.CheckInterval);
        _loopTask = RunLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delegation supervision service stopping");

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

        _logger.LogInformation("Delegation supervision service stopped");
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
                await CheckOverdueAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during supervision check");
            }
        }
    }
}
