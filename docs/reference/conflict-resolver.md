# ConflictResolver & ConflictNotResolved

Interface for domain-level conflict resolution and the unresolvable conflict marker.

## ConflictResolver Interface

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

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`
**Extends:** `Picea.Decider<TState, TCommand, TEvent, TEffect, TError, TParameters>`

### ResolveConflicts

```csharp
static abstract Result<TEvent[], ConflictNotResolved> ResolveConflicts(
    TState currentState,
    TState projectedState,
    TEvent[] ourEvents,
    IReadOnlyList<TEvent> theirEvents)
```

Called by `ResolvingAggregateRunner` when a `ConcurrencyException` occurs during `Handle`.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `currentState` | `TState` | State after applying *their* events to the base state |
| `projectedState` | `TState` | State after applying *our* events on top of `currentState` |
| `ourEvents` | `TEvent[]` | Events we tried to append |
| `theirEvents` | `IReadOnlyList<TEvent>` | Events written by others since our last known version |

**Returns:**
- `Ok(events)` — Resolved. The runner will retry `AppendAsync` with these events.
- `Err(ConflictNotResolved)` — Unresolvable. The runner throws `ConcurrencyException`.

---

## ConflictNotResolved

```csharp
public readonly record struct ConflictNotResolved(string Reason);
```

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`

A value type indicating that a concurrency conflict could not be resolved at the domain level.

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Reason` | `string` | Human-readable explanation of why resolution failed |

## Example

```csharp
public class CounterDecider
    : ConflictResolver<CounterState, CounterCommand, CounterEvent,
        CounterEffect, CounterError, Unit>
{
    public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
        CounterState currentState,
        CounterState projectedState,
        CounterEvent[] ourEvents,
        IReadOnlyList<CounterEvent> theirEvents)
    {
        // Commutative — safe to replay unchanged
        if (projectedState.Count is >= 0 and <= MaxCount)
            return Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents);

        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved($"Projected count {projectedState.Count} out of bounds"));
    }

    // ... Initialize, Decide, Transition ...
}
```

## See Also

- [Conflict Resolution Concept](../concepts/conflict-resolution.md)
- [Tutorial 03: Conflict Resolution](../tutorials/03-conflict-resolution.md)
- [Conflict Resolution Patterns](../guides/conflict-resolution-patterns.md)
- [ResolvingAggregateRunner](resolving-aggregate-runner.md)
