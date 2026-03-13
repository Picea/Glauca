# Conflict Resolution Patterns

Common strategies for resolving concurrent write conflicts in event-sourced systems.

## When Do You Need This?

Use `ResolvingAggregateRunner` when:

- Multiple processes or users can modify the same stream concurrently
- Some operations are commutative (order doesn't matter)
- You want automatic retry instead of failing on first conflict

If your system is single-writer per stream, stick with `AggregateRunner` — it's simpler.

## The Resolution API

```csharp
static Result<TEvent[], ConflictNotResolved> ResolveConflicts(
    TState currentState,      // State after their events applied
    TState projectedState,    // State after our events applied on top
    TEvent[] ourEvents,       // Events we tried to append
    IReadOnlyList<TEvent> theirEvents  // Events written by others since our load
);
```

You return `Ok(events)` to retry the append with the given events, or `Err(ConflictNotResolved)` to give up.

## Pattern 1: Commutative Operations

**When:** All operations commute — the result is the same regardless of order.

**Example:** Incrementing and decrementing a counter.

```csharp
public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
    CounterState currentState,
    CounterState projectedState,
    CounterEvent[] ourEvents,
    IReadOnlyList<CounterEvent> theirEvents)
{
    // Increments and decrements commute: 3+5 = 5+3
    // Just validate the merged result is still valid
    if (projectedState.Count < 0)
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Would go negative"));

    if (projectedState.Count > MaxCount)
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Would exceed maximum"));

    return Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents);
}
```

**Key insight:** If all events commute, you can always replay your events unchanged. Just check the merged result.

## Pattern 2: Partial Commutativity

**When:** Some operations commute and others don't.

**Example:** Increment/decrement commutes, but Reset does not.

```csharp
public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
    CounterState currentState,
    CounterState projectedState,
    CounterEvent[] ourEvents,
    IReadOnlyList<CounterEvent> theirEvents)
{
    // Non-commutative operations cannot be auto-resolved
    if (ourEvents.Any(e => e is CounterEvent.WasReset) ||
        theirEvents.Any(e => e is CounterEvent.WasReset))
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Reset is not commutative"));

    // Remaining operations are commutative — safe to merge
    if (projectedState.Count is < 0 or > MaxCount)
        return Result<CounterEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Out of bounds"));

    return Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents);
}
```

**Key insight:** Check both `ourEvents` and `theirEvents` for non-commutative operations. If either side has one, conflict is unresolvable.

## Pattern 3: Last-Writer-Wins

**When:** The most recent write should always win (e.g., profile updates).

```csharp
public static Result<ProfileEvent[], ConflictNotResolved> ResolveConflicts(
    ProfileState currentState,
    ProfileState projectedState,
    ProfileEvent[] ourEvents,
    IReadOnlyList<ProfileEvent> theirEvents)
{
    // Our events always win — we're the latest writer
    // Re-derive events against the current state
    var newEvents = ourEvents
        .Select(e => e switch
        {
            ProfileEvent.NameChanged n => n,  // idempotent
            ProfileEvent.EmailChanged m => m, // idempotent
            _ => e
        })
        .ToArray();

    return Result<ProfileEvent[], ConflictNotResolved>.Ok(newEvents);
}
```

**Caution:** This can lose concurrent work. Use only when that's acceptable.

## Pattern 4: Merge by Field

**When:** Different fields can be updated independently.

```csharp
public static Result<DocumentEvent[], ConflictNotResolved> ResolveConflicts(
    DocumentState currentState,
    DocumentState projectedState,
    DocumentEvent[] ourEvents,
    IReadOnlyList<DocumentEvent> theirEvents)
{
    // Check for conflicting field updates
    var ourFields = ourEvents.Select(FieldOf).ToHashSet();
    var theirFields = theirEvents.Select(FieldOf).ToHashSet();

    if (ourFields.Overlaps(theirFields))
        return Result<DocumentEvent[], ConflictNotResolved>.Err(
            new ConflictNotResolved("Concurrent updates to same field"));

    // No field overlap — safe to merge
    return Result<DocumentEvent[], ConflictNotResolved>.Ok(ourEvents);
}
```

**Key insight:** Non-overlapping field updates are always safe to merge.

## Pattern 5: Always Fail

**When:** Concurrent writes are never acceptable (financial transactions, regulatory requirements).

```csharp
public static Result<TEvent[], ConflictNotResolved> ResolveConflicts(
    TState currentState,
    TState projectedState,
    TEvent[] ourEvents,
    IReadOnlyList<TEvent> theirEvents)
{
    return Result<TEvent[], ConflictNotResolved>.Err(
        new ConflictNotResolved("Concurrent modification not allowed"));
}
```

This makes `ResolvingAggregateRunner` behave exactly like `AggregateRunner` — useful if you want the runner infrastructure but never auto-resolve.

## Retry Budget

`ResolvingAggregateRunner` retries up to **3 times** (constant `MaxRetries = 3`). If resolution succeeds but the retry also conflicts (another concurrent writer), it catches up again and retries. After 3 failures, the original `ConcurrencyException` propagates.

## Decision Tree

```text
Can this operation ever conflict?
    │
    ├─ No → Use AggregateRunner (simpler)
    │
    └─ Yes → Do all operations commute?
        │
        ├─ Yes → Pattern 1: Commutative
        │
        └─ No → Are some operations commutative?
            │
            ├─ Yes → Pattern 2: Partial Commutativity
            │
            └─ No → Is last-write-wins acceptable?
                │
                ├─ Yes → Pattern 3: Last-Writer-Wins
                │
                └─ No → Can writes be partitioned by field?
                    │
                    ├─ Yes → Pattern 4: Merge by Field
                    │
                    └─ No → Pattern 5: Always Fail
```

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| See the theory | [Conflict Resolution Concept](../concepts/conflict-resolution.md) |
| Walk through an example | [Tutorial 03: Conflict Resolution](../tutorials/03-conflict-resolution.md) |
| See the API | [ConflictResolver Reference](../reference/conflict-resolver.md) |
