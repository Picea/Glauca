# Tutorial 03: Conflict Resolution

Build a shared counter with automatic concurrent write resolution.

## What You'll Learn

- Implement a `ConflictResolver` for commutative operations
- Use `ResolvingAggregateRunner` for automatic conflict resolution
- Distinguish commutative from non-commutative operations
- Handle unresolvable conflicts

## Prerequisites

Complete [Tutorial 01: First Aggregate](01-first-aggregate.md).

## The Problem

Two processes load the same counter at version 3 and both try to increment:

```text
Process A: Load(version=3) → Add(2) → Append at version 3 ✅ (now version 5)
Process B: Load(version=3) → Add(1) → Append at version 3 ❌ ConcurrencyException!
```

With `AggregateRunner`, Process B fails. The caller must retry manually.

With `ResolvingAggregateRunner`, Process B's conflict is resolved automatically.

## Step 1: Implement ConflictResolver

Extend your decider to implement `ConflictResolver` instead of `Decider`:

```csharp
using Picea;
using Picea.Glauca;

public class CounterDecider
    : ConflictResolver<CounterState, CounterCommand, CounterEvent,
        CounterEffect, CounterError, Unit>
{
    public const int MaxCount = 100;

    // Initialize, Decide, Transition — same as before (see Tutorial 01)

    public static (CounterState, CounterEffect) Initialize(Unit _) =>
        (new CounterState(0), new CounterEffect.None());

    public static Result<CounterEvent[], CounterError> Decide(
        CounterState state, CounterCommand command) =>
        command switch
        {
            CounterCommand.Add(var n) when state.Count + n > MaxCount =>
                Result<CounterEvent[], CounterError>
                    .Err(new CounterError.Overflow(state.Count, n, MaxCount)),
            CounterCommand.Add(var n) when state.Count + n < 0 =>
                Result<CounterEvent[], CounterError>
                    .Err(new CounterError.Underflow(state.Count, n)),
            CounterCommand.Add(var n) when n >= 0 =>
                Result<CounterEvent[], CounterError>
                    .Ok(Enumerable.Repeat<CounterEvent>(
                        new CounterEvent.Incremented(), n).ToArray()),
            CounterCommand.Add(var n) =>
                Result<CounterEvent[], CounterError>
                    .Ok(Enumerable.Repeat<CounterEvent>(
                        new CounterEvent.Decremented(), Math.Abs(n)).ToArray()),
            _ => throw new UnreachableException()
        };

    public static (CounterState, CounterEffect) Transition(
        CounterState state, CounterEvent @event) =>
        @event switch
        {
            CounterEvent.Incremented =>
                (state with { Count = state.Count + 1 }, new CounterEffect.None()),
            CounterEvent.Decremented =>
                (state with { Count = state.Count - 1 }, new CounterEffect.None()),
            _ => throw new UnreachableException()
        };

    // NEW: Resolve concurrency conflicts
    public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
        CounterState currentState,
        CounterState projectedState,
        CounterEvent[] ourEvents,
        IReadOnlyList<CounterEvent> theirEvents)
    {
        // Increments and decrements are commutative:
        // Add(3) then Add(5) = Add(5) then Add(3) = 8
        // So we can safely replay our events on the merged state.
        // Just validate the result stays within bounds.

        return projectedState.Count switch
        {
            > MaxCount => Result<CounterEvent[], ConflictNotResolved>.Err(
                new ConflictNotResolved(
                    $"Count {projectedState.Count} would exceed max {MaxCount}")),

            < 0 => Result<CounterEvent[], ConflictNotResolved>.Err(
                new ConflictNotResolved(
                    $"Count {projectedState.Count} would go below zero")),

            _ => Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents)
        };
    }
}
```

## Step 2: Use ResolvingAggregateRunner

```csharp
var store = new InMemoryEventStore<CounterEvent>();

// Use ResolvingAggregateRunner instead of AggregateRunner
using var counter = ResolvingAggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);
```

The API is identical to `AggregateRunner` — same `Create`, `Load`, `Handle`, `Rebuild`. The difference is under the hood: when `ConcurrencyException` occurs during `Handle`, it catches it and runs the resolution logic.

## Step 3: Simulate Concurrent Writes

```csharp
// Simulate two processes loading the same aggregate
using var processA = ResolvingAggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-shared", default);

using var processB = ResolvingAggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-shared", default);

// Both start at version 0
// Process A increments by 3
await processA.Handle(new CounterCommand.Add(3));
// Stream is now at version 3

// Process B also tries to increment by 2 — it was loaded at version 0!
// Without resolution: ConcurrencyException
// With resolution: automatically catches up, validates, retries
await processB.Handle(new CounterCommand.Add(2));

// Both see the correct merged state
Console.WriteLine($"Process A: {processA.State.Count}"); // 3
Console.WriteLine($"Process B: {processB.State.Count}"); // 5 (3 + 2, merged)
```

## Step 4: Handle Non-Commutative Operations

Not all operations commute. A `Reset` depends on the exact count — it cannot be merged:

```csharp
public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
    CounterState currentState,
    CounterState projectedState,
    CounterEvent[] ourEvents,
    IReadOnlyList<CounterEvent> theirEvents)
{
    // Reset is non-commutative — cannot auto-resolve
    if (ourEvents.Any(e => e is CounterEvent.WasReset))
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Reset conflicts with concurrent changes"));

    if (theirEvents.Any(e => e is CounterEvent.WasReset))
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Cannot apply after concurrent reset"));

    // Commutative operations — validate merged result
    return projectedState.Count switch
    {
        > MaxCount => Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved($"Would exceed max")),
        < 0 => Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved($"Would go below zero")),
        _ => Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents)
    };
}
```

When resolution fails, `ConcurrencyException` is thrown — just like `AggregateRunner`.

## What You Built

```text
Process A: Add(3) ─────────────────────▶ Append [I,I,I] at v0 ✅ v=3
Process B: Add(2) ─────────────────────▶ Append [I,I] at v0   ❌ Conflict!
                                            │
                                            ├─ Load their events [I,I,I]
                                            ├─ Apply: state = 3
                                            ├─ Project ours: state = 5
                                            ├─ ResolveConflicts(3, 5, ours, theirs)
                                            │   └─ 5 ≤ MaxCount → Ok(ourEvents)
                                            └─ Retry: Append [I,I] at v3 ✅ v=5
```

## What's Next

| If you want to… | Read |
| ---------------- | ---- |
| Coordinate across aggregates | [Tutorial 04: Sagas](04-sagas.md) |
| See more resolution strategies | [Conflict Resolution Patterns](../guides/conflict-resolution-patterns.md) |
| Understand the theory | [Conflict Resolution Concept](../concepts/conflict-resolution.md) |
