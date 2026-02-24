namespace Cortex.Core.References;

/// <summary>
/// Generates unique reference codes for message tracking.
/// </summary>
public interface IReferenceCodeGenerator
{
    /// <summary>
    /// Generates a new unique reference code.
    /// </summary>
    Task<ReferenceCode> GenerateAsync(CancellationToken cancellationToken = default);
}
