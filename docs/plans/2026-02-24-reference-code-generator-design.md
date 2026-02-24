# Reference Code Generator — Design Document

**Date:** 2026-02-24
**Author:** Daniel Grimes
**Status:** Approved
**Issue:** #7

## Context

Every message in Cortex carries a `ReferenceCode` — a unique tracking ID in `CTX-YYYY-MMDD-NNN` format that traces work across agents, queues, and logs. The `ReferenceCode` value object and `IReferenceCodeGenerator` interface exist but have no concrete implementation. This design covers the generator implementation with persistent sequence tracking.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Persistence | Pluggable `ISequenceStore` interface with file-based default | Keeps generator testable; swap for database later without touching generator |
| Thread safety | `SemaphoreSlim(1,1)` in the generator | Serialises concurrent access; keeps store implementations simple |
| Interface | Async `GenerateAsync(CancellationToken)` | File I/O needs async; project convention is async + CancellationToken on all interfaces |
| File path | Configurable via `FileSequenceStoreOptions` | Sensible default, host decides location |
| Overflow | Widen format to 3-4 digits, cap at 9999 | Backward compatible — existing 3-digit codes still valid |

## Components

| Type | Project | Purpose |
|------|---------|---------|
| `ISequenceStore` | Cortex.Core | Pluggable persistence — load/save current sequence and date |
| `FileSequenceStore` | Cortex.Core | File-based implementation with configurable path |
| `InMemorySequenceStore` | Cortex.Core | For unit tests and local development |
| `SequentialReferenceCodeGenerator` | Cortex.Core | Implements `IReferenceCodeGenerator` — wires clock + store |
| `FileSequenceStoreOptions` | Cortex.Core | Options class with `FilePath` property |

## Interface Changes

### IReferenceCodeGenerator

```csharp
public interface IReferenceCodeGenerator
{
    Task<ReferenceCode> GenerateAsync(CancellationToken cancellationToken = default);
}
```

Breaking change from `ReferenceCode Generate()`. Few consumers exist — all updated as part of this work.

### ReferenceCode

- Regex widened from `\d{3}` to `\d{3,4}` to support overflow
- `Create()` cap raised from 999 to 9999
- Format uses `D3` for sequences 1-999, `D4` for 1000-9999

Backward compatible — all existing `CTX-YYYY-MMDD-NNN` codes remain valid.

## ISequenceStore Contract

```csharp
public interface ISequenceStore
{
    Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default);
}

public record SequenceState(DateOnly Date, int Sequence);
```

## Data Flow

```
GenerateAsync called
  → Acquire SemaphoreSlim
  → Load (date, sequence) from ISequenceStore
  → If date ≠ today → reset sequence to 0
  → Increment sequence
  → Save (today, newSequence) to ISequenceStore
  → Release SemaphoreSlim
  → Return ReferenceCode.Create(today, newSequence)
```

## File Format

```json
{ "date": "2026-02-24", "sequence": 42 }
```

## Testing Strategy

- `SequentialReferenceCodeGenerator` tested with `InMemorySequenceStore`
- `FileSequenceStore` tested with temp files
- Concurrency tests — parallel `GenerateAsync` calls produce unique sequences
- Daily reset — mock clock to verify sequence resets on date change
- Overflow — verify sequences 1000+ produce 4-digit codes

## Exclusions

- No distributed locking — single-process only
- No database-backed store — file-based is the only shipped implementation
- No auto-cleanup of the sequence file
