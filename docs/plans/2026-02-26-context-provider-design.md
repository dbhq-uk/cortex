# IContextProvider Design

**Issue:** #25 — IContextProvider: business context for orchestration
**Date:** 2026-02-26
**Status:** Approved

## Problem

The CoS agent triages messages using only message content and available capabilities. It has no mechanism to incorporate business context — customer history, meeting notes, past decisions, lessons learned. Without this, triage prompts are generic rather than informed.

## Approach

Structured query object (Approach B from brainstorming). A `ContextQuery` record with optional filters replaces a free-text string, giving type-safe category/tag/reference filtering without interface bloat.

## Types

All new types live in `Cortex.Core.Context`.

### ContextCategory (enum)

```
CustomerNote, MeetingNote, Decision, Lesson, Preference, Strategic, Operational
```

### ContextEntry (sealed record)

| Property | Type | Required | Notes |
|----------|------|----------|-------|
| EntryId | string | yes | Unique identifier |
| Content | string | yes | The context text |
| Category | ContextCategory | yes | Classification |
| Tags | IReadOnlyList\<string\> | no | Default `[]` |
| ReferenceCode | ReferenceCode? | no | Links to message thread |
| CreatedAt | DateTimeOffset | yes | When stored |

### ContextQuery (sealed record)

| Property | Type | Notes |
|----------|------|-------|
| Keywords | string? | Case-insensitive substring match on Content |
| Category | ContextCategory? | Exact match filter |
| Tags | IReadOnlyList\<string\>? | Any-overlap match |
| ReferenceCode | ReferenceCode? | Exact match filter |
| MaxResults | int? | Limit result count |

All filters combine with AND semantics. Null/empty filters are ignored.

### IContextProvider (interface)

```csharp
Task<IReadOnlyList<ContextEntry>> QueryAsync(ContextQuery query, CancellationToken cancellationToken = default);
Task StoreAsync(ContextEntry entry, CancellationToken cancellationToken = default);
```

## Implementations

### InMemoryContextProvider

Thread-safe in-memory store using `ConcurrentDictionary<string, ContextEntry>`. Used for unit testing and local development.

### FileContextProvider

Reads and writes markdown files in a configurable `context/` directory. One file per entry, YAML front matter for metadata, body for content.

File format:
```markdown
---
entryId: abc-123
category: CustomerNote
tags: [smith-project, pricing]
referenceCode: CTX-2026-0226-001
createdAt: 2026-02-26T10:00:00Z
---
Customer prefers monthly billing. Discussed in Feb meeting.
```

Query matching:
- Category: exact enum match
- Tags: any overlap (entry has at least one matching tag)
- ReferenceCode: exact match
- Keywords: case-insensitive substring on Content
- All filters AND together; null filters ignored

Directory creation on first write. Missing directory on read returns empty results.

## Integration with SkillDrivenAgent

`IContextProvider` is injected into `SkillDrivenAgent` as an optional dependency. During `ProcessAsync`, the agent queries context using keywords extracted from the message content and passes results as a `businessContext` parameter to the skill pipeline. Skills (starting with `cos-triage`) can then incorporate business context in their prompts.

The `cos-context-query` skill (executor type `csharp`) is deferred until a `csharp` skill executor exists. Direct injection delivers the same value now without blocking on executor infrastructure.

## Out of Scope

- Semantic/vector search — keyword matching is sufficient for Phase 1
- `csharp` skill executor — separate issue
- Context expiry/TTL
- Access control on context entries
- Context versioning or audit trail

## Testing

### InMemoryContextProvider
- Store then query round-trip
- Keyword matching (case-insensitive, partial match)
- Category filtering
- Tag filtering (any overlap)
- Reference code filtering
- Combined filters (AND semantics)
- Empty results when no match
- Multiple results ordering

### FileContextProvider
- Store creates markdown file with YAML front matter
- Query reads and parses files correctly
- All filter types work across files
- Missing directory handled gracefully (empty results on read, created on write)
- Round-trip: store then query preserves all fields

## Dependencies

- Issue #24 (CoS agent with skill pipeline) — merged
- Issue #29 (AuthorityClaims serialisation fix) — fixed this session
