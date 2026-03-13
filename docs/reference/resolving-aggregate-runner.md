# ResolvingAggregateRunner

Aggregate runtime with automatic conflict resolution. Extends `AggregateRunner` behaviour with catch-up and retry on `ConcurrencyException`.

## Class Definition

```csharp
public sealed class ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
    : IDisposable
    where TDecider : ConflictResolver<TState, TCommand, TEvent, TEffect, TError, TParameters>
```

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`
**Implements:** `IDisposable`

## Type Parameters

Same as [`AggregateRunner`](aggregate-runner.md), except `TDecider` must implement `ConflictResolver` instead of `Decider`.

## Factory Methods

### Create

```csharp
public static ResolvingAggregateRunner<...> Create(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters)
```

Identical to `AggregateRunner.Create`.

### Load

```csharp
public static async ValueTask<ResolvingAggregateRunner<...>> Load(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters,
    CancellationToken ct = default)
```

Identical to `AggregateRunner.Load`.

## Properties

Same as [`AggregateRunner`](aggregate-runner.md): `StreamId`, `Parameters`, `State`, `Version`, `Effects`, `IsTerminal`.

## Methods

### Handle

```csharp
public async ValueTask<Result<TState, TError>> Handle(
    TCommand command, CancellationToken ct = default)
```

Same decide → persist → transition cycle as `AggregateRunner`, plus conflict resolution:

1. Calls `TDecider.Decide(State, command)`
2. If `Ok`: attempts `EventStore.AppendAsync`
3. On `ConcurrencyException`:
   a. Loads events written since our version
   b. Applies them to get `currentState`
   c. Projects our events on top to get `projectedState`
   d. Calls `TDecider.ResolveConflicts(currentState, projectedState, ourEvents, theirEvents)`
   e. If resolved: retries append with resolved events (up to `MaxRetries`)
   f. If not resolved: throws `ConcurrencyException`

### Rebuild

```csharp
public async ValueTask<TState> Rebuild(CancellationToken ct = default)
```

Identical to `AggregateRunner.Rebuild`.

### Dispose

```csharp
public void Dispose()
```

Releases the internal `SemaphoreSlim`.

## Constants

| Constant | Value | Description |
| -------- | ----- | ----------- |
| `MaxRetries` | `3` | Maximum resolution retry attempts |

## Resolution Flow

```text
Handle(command)
    │
    ├─ Decide(state, command) → Err → return Err
    │
    ├─ Decide → Ok(events)
    │   │
    │   ├─ AppendAsync → ✅ → update state → return Ok
    │   │
    │   └─ AppendAsync → ConcurrencyException
    │       │
    │       ├─ Load their events since our version
    │       ├─ Apply their events: currentState
    │       ├─ Project our events: projectedState
    │       ├─ ResolveConflicts(current, projected, ours, theirs)
    │       │   │
    │       │   ├─ Ok(resolvedEvents) → retry AppendAsync (up to 3×)
    │       │   │
    │       │   └─ Err → throw ConcurrencyException
    │       │
    │       └─ 3 retries exhausted → throw ConcurrencyException
```

## Example

```csharp
var store = new InMemoryEventStore<CounterEvent>();

using var runner = ResolvingAggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);

// If another process writes to "counter-1" concurrently,
// the runner automatically resolves using CounterDecider.ResolveConflicts
var result = await runner.Handle(new CounterCommand.Add(3));
```

## See Also

- [ConflictResolver](conflict-resolver.md)
- [Conflict Resolution Concept](../concepts/conflict-resolution.md)
- [Tutorial 03: Conflict Resolution](../tutorials/03-conflict-resolution.md)
- [Conflict Resolution Patterns](../guides/conflict-resolution-patterns.md)
