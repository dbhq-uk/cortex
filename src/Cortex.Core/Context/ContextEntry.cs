using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// A single business context entry — a piece of accumulated wisdom
/// (customer history, meeting note, decision, lesson learned) that
/// enriches orchestration and triage.
/// </summary>
public sealed record ContextEntry
{
    /// <summary>Unique identifier for this entry.</summary>
    public required string EntryId { get; init; }

    /// <summary>The context text content.</summary>
    public required string Content { get; init; }

    /// <summary>Classification category.</summary>
    public required ContextCategory Category { get; init; }

    /// <summary>Searchable tags for this entry.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Optional link to a message thread reference code.</summary>
    public ReferenceCode? ReferenceCode { get; init; }

    /// <summary>When this entry was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
