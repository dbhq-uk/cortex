namespace Cortex.Agents.Supervision;

/// <summary>
/// Configuration for the DelegationSupervisionService.
/// </summary>
public sealed record SupervisionOptions
{
    /// <summary>Interval between supervision checks. Default: 60 seconds.</summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum retry attempts before escalating. Default: 3.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Queue to publish supervision alerts to. Default: agent.cos.</summary>
    public string AlertTarget { get; init; } = "agent.cos";

    /// <summary>Queue to publish escalation alerts to. Default: agent.founder.</summary>
    public string EscalationTarget { get; init; } = "agent.founder";
}
