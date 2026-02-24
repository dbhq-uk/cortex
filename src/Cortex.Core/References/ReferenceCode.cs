using System.Text.RegularExpressions;

namespace Cortex.Core.References;

/// <summary>
/// A unique traceable reference code that follows a thread across all systems.
/// Format: CTX-YYYY-MMDD-NNN or CTX-YYYY-MMDD-NNNN (e.g. CTX-2026-0221-001, CTX-2026-0221-1000)
/// </summary>
public readonly partial record struct ReferenceCode
{
    private static readonly Regex Pattern = ReferenceCodePattern();

    /// <summary>
    /// The reference code value.
    /// </summary>
    public string Value { get; }

    public ReferenceCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"Reference code must match pattern CTX-YYYY-MMDD-NNN(N). Got: {value}",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new reference code for the given date and sequence number.
    /// </summary>
    public static ReferenceCode Create(DateTimeOffset date, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, 9999);

        var sequencePart = sequence > 999 ? $"{sequence:D4}" : $"{sequence:D3}";
        var value = $"CTX-{date.Year:D4}-{date.Month:D2}{date.Day:D2}-{sequencePart}";
        return new ReferenceCode(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^CTX-\d{4}-\d{4}-\d{3,4}$")]
    private static partial Regex ReferenceCodePattern();
}
