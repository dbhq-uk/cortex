using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// Structured query for searching business context entries.
/// All filters combine with AND semantics. Null/empty filters are ignored.
/// </summary>
public sealed record ContextQuery
{
    /// <summary>Case-insensitive substring match on entry content.</summary>
    public string? Keywords { get; init; }

    /// <summary>Exact category filter.</summary>
    public ContextCategory? Category { get; init; }

    /// <summary>Tag overlap filter — matches entries that have at least one of these tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Exact reference code filter.</summary>
    public ReferenceCode? ReferenceCode { get; init; }

    /// <summary>Maximum number of results to return. Null means no limit.</summary>
    public int? MaxResults { get; init; }
}
