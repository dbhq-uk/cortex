using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// Stores decomposition plans that are awaiting human approval (AskMeFirst gate).
/// </summary>
public interface IPendingPlanStore
{
    /// <summary>
    /// Stores a pending plan keyed by its workflow reference code.
    /// </summary>
    Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a pending plan by its workflow reference code, or null if not found.
    /// </summary>
    Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a pending plan by its workflow reference code.
    /// </summary>
    Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);
}
