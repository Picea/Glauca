# Event Sourcing

## What Is Event Sourcing?

Event sourcing is a persistence pattern where the source of truth is an append-only sequence of **domain events** rather than a mutable current-state record. Instead of storing "the counter is at 5", you store the five `Incremented` events that produced that value.

```text
Traditional (State Sourcing):
    ┌─────────────────┐
    │ Counter: 5      │  ← mutable row, overwritten on every change
    └─────────────────┘

Event Sourcing:
    ┌──────────────┐
    │ Incremented  │ seq=1
    │ Incremented  │ seq=2
    │ Incremented  │ seq=3
    │ Decremented  │ seq=4
    │ Incremented  │ seq=5
    │ Incremented  │ seq=6
    └──────────────┘  ← append-only log, current state = fold(events)
```

To get the current state, you **replay** the events through a pure `Transition` function — a left fold:

$$\text{state} = \text{fold}(\text{transition}, \text{initial}, \text{events})$$

## Why Event Sourcing?

| Benefit | Explanation |
| ------- | ----------- |
| **Complete audit trail** | Every state change is recorded with a timestamp. You can reconstruct the state at any point in time. |
| **Temporal queries** | "What was the account balance at 3pm yesterday?" is a fold over events up to that timestamp. |
| **Multiple read models** | The same event stream can power different projections — a summary view, a detailed view, an analytics pipeline. |
| **Debugging** | When something goes wrong, you have the full causal history. No guessing what happened. |
| **Decoupled writes and reads** | Commands produce events (write side). Projections consume events (read side). They evolve independently. |

## How Glauca Implements Event Sourcing

Glauca's event sourcing is built on two Picea kernel concepts:

1. **`Decider`** — The pure domain logic. `Decide(state, command) → events` and `Transition(state, event) → state`.
2. **`EventStore`** — The persistence abstraction. `AppendAsync` and `LoadAsync` with optimistic concurrency.

The `AggregateRunner` bridges them:

```text
                    ┌──────────────┐
  Command ─────────▶│              │
                    │  AggregateRunner  │
                    │              │
  ┌─ Decide ───────▶│ 1. Decide    │──── Domain Error?  ──▶ Return Err
  │                 │ 2. Append    │──── Version conflict? ──▶ ConcurrencyException
  │                 │ 3. Transition│
  │                 └──────────────┘
  │                        │
  │                        ▼
  │                   EventStore
  │                   (append-only)
  │
  └─── Pure function: no I/O, no side effects
```

### The Command Handling Loop

1. **Decide** — The decider validates the command against the current state. Returns `Ok(events)` or `Err(error)`.
2. **Append** — Events are appended to the event store with optimistic concurrency control (`expectedVersion`).
3. **Transition** — Each persisted event is applied to the in-memory state via the pure `Transition` function.

This is a **monadic left fold** — the same mathematical structure as the Picea kernel's runtime loop, but with persistence inserted between decision and transition.

### State Reconstruction

When loading an aggregate, the runner replays all events through `Transition`:

```csharp
var (state, _) = TDecider.Initialize(parameters);
var events = await store.LoadAsync(streamId, ct);

foreach (var stored in events)
{
    var (newState, effect) = TDecider.Transition(state, stored.Event);
    state = newState;
}
```

This guarantees that the in-memory state is always consistent with the persisted events.

## The Event Store Contract

The `EventStore<TEvent>` interface defines three operations:

| Method | Purpose |
| ------ | ------- |
| `AppendAsync` | Append events with optimistic concurrency (`expectedVersion`) |
| `LoadAsync(streamId)` | Load all events from a stream |
| `LoadAsync(streamId, afterVersion)` | Load events after a given version (for catch-up) |

Optimistic concurrency is enforced via version checking:
- The caller provides the `expectedVersion` (the version they last read)
- If the stream has been modified since, `ConcurrencyException` is thrown
- No events are partially written — the append is atomic

## When to Use Event Sourcing

✅ **Good fit:**
- Audit-heavy domains (finance, healthcare, compliance)
- Systems that need temporal queries ("what was the state at time T?")
- CQRS architectures with multiple read models
- Domains where the "why" matters as much as the "what"

⚠️ **Consider carefully:**
- Simple CRUD with no audit requirements
- Very high-throughput writes with no need for event history
- Domains where events are meaningless (e.g., raw sensor data at millisecond granularity)

## See Also

- [The Aggregate Runner](aggregate-runner.md) — how commands become persisted events
- [Projections](projections.md) — building read models from events
- [Conflict Resolution](conflict-resolution.md) — handling concurrent writes
- [EventStore Reference](../reference/event-store.md) — complete API documentation
