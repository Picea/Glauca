# Conflict Resolution

## The Problem

In event-sourced systems, optimistic concurrency control protects against lost writes. When two processes try to append events to the same stream concurrently, one will succeed and the other will get a `ConcurrencyException`:

```text
Process A: Load(version=3) → Decide → Append(expectedVersion=3) ✅ Success (now version=5)
Process B: Load(version=3) → Decide → Append(expectedVersion=3) ❌ ConcurrencyException!
```

Process B's write is rejected because the stream has moved from version 3 to version 5 since it last read.

The default behavior (`AggregateRunner`) throws `ConcurrencyException` and lets the caller decide what to do — retry, fail, or escalate.

But in many domains, the conflict can be **resolved automatically** by examining the concurrent events.

## Domain-Aware Resolution

The `ResolvingAggregateRunner` extends `AggregateRunner` with automatic conflict resolution. When a `ConcurrencyException` occurs, the runner:

1. **Loads** the events that were concurrently appended ("their events")
2. **Applies** their events to get the current state
3. **Projects** our events on top to get the projected state
4. **Asks** the domain to resolve via `ResolveConflicts`
5. **Retries** the append with the resolved events (up to 3 times)

```text
Process A: Append [Incremented, Incremented]  → version 3 → 5 ✅
Process B: Append [Incremented] at version 3  → ConcurrencyException!
           │
           ├─ Load their events: [Incremented, Incremented] (seq 4, 5)
           ├─ Apply to state: count = 5
           ├─ Project our events: count = 6
           ├─ ResolveConflicts(currentState=5, projectedState=6, ours, theirs)
           │   └─ Increments are commutative → Ok(ourEvents)
           └─ Retry: Append [Incremented] at version 5 ✅
```

## The ConflictResolver Interface

To use automatic conflict resolution, your decider implements `ConflictResolver` instead of `Decider`:

```csharp
public interface ConflictResolver<TState, TCommand, TEvent, TEffect, TError, TParameters>
    : Decider<TState, TCommand, TEvent, TEffect, TError, TParameters>
{
    static abstract Result<TEvent[], ConflictNotResolved> ResolveConflicts(
        TState currentState,
        TState projectedState,
        TEvent[] ourEvents,
        IReadOnlyList<TEvent> theirEvents);
}
```

The parameters provide all the context needed for resolution:

| Parameter | What It Is |
| --------- | ---------- |
| `currentState` | State after applying *their* events to the last known state |
| `projectedState` | State after applying *our* events on top of `currentState` |
| `ourEvents` | The events we tried to append |
| `theirEvents` | The events that were concurrently appended |

Return values:

| Return | Meaning |
| ------ | ------- |
| `Ok(events)` | Conflict resolved — retry appending these events (may be same as `ourEvents` or modified) |
| `Err(ConflictNotResolved)` | Conflict cannot be resolved — throws `ConcurrencyException` |

## Commutativity: The Key Insight

The resolution strategy depends on whether the operations **commute**:

$$f(g(x)) = g(f(x))$$

If applying our events and their events produces the same result regardless of order, the operations commute and can be safely replayed.

### Commutative: Counter Increments

Two independent `Add(3)` and `Add(5)` commands both produce `Incremented` events. The final count is the same regardless of order:

```csharp
public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
    CounterState currentState,
    CounterState projectedState,
    CounterEvent[] ourEvents,
    IReadOnlyList<CounterEvent> theirEvents)
{
    // Increments/decrements are commutative — validate the merged result
    return projectedState.Count switch
    {
        > MaxCount => Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Would exceed maximum")),
        < 0 => Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Would go below zero")),
        _ => Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents)
    };
}
```

### Non-Commutative: Reset

A `Reset` event sets the counter to zero. It depends on the exact count at the time of reset — it does not commute with increments:

```csharp
// Reset is non-commutative — cannot be resolved
if (ourEvents.Any(e => e is CounterEvent.WasReset))
    return Result<CounterEvent[], ConflictNotResolved>.Err(
        new ConflictNotResolved("Reset conflicts with concurrent changes."));
```

## Retry Budget

The `ResolvingAggregateRunner` retries up to **3 times** (`MaxRetries = 3`). If the conflict cannot be resolved after 3 attempts (e.g., extremely high contention), `ConcurrencyException` is thrown.

This prevents infinite retry loops in pathological contention scenarios.

## When to Use Which Runner

| Runner | Use When |
| ------ | -------- |
| `AggregateRunner` | Conflicts are rare or you want explicit control over retry logic |
| `ResolvingAggregateRunner` | Your domain has commutative operations that can be auto-merged |

## See Also

- [Conflict Resolution Patterns Guide](../guides/conflict-resolution-patterns.md) — common resolution strategies
- [ConflictResolver Reference](../reference/conflict-resolver.md) — complete API documentation
- [ResolvingAggregateRunner Reference](../reference/resolving-aggregate-runner.md) — runner API
