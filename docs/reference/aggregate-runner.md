# AggregateRunner

Event-sourced aggregate runtime with persistence and optimistic concurrency.

## Class Definition

```csharp
public sealed class AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
    : IDisposable
    where TDecider : Decider<TState, TCommand, TEvent, TEffect, TError, TParameters>
```

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`
**Implements:** `IDisposable`

## Type Parameters

| Parameter | Constraint | Description |
| --------- | ---------- | ----------- |
| `TDecider` | `Decider<...>` | Decider implementing domain logic |
| `TState` | — | Aggregate state |
| `TCommand` | — | Command type |
| `TEvent` | — | Event type |
| `TEffect` | — | Effect type |
| `TError` | — | Error type |
| `TParameters` | — | Initialization parameters |

## Factory Methods

### Create

```csharp
public static AggregateRunner<...> Create(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters)
```

Creates a new aggregate instance. Initializes state via `TDecider.Initialize(parameters)` with version `0`. Does **not** check whether the stream already exists — use `Load` for existing streams.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `store` | `EventStore<TEvent>` | Event store for persistence |
| `streamId` | `string` | Unique stream identifier |
| `parameters` | `TParameters` | Passed to `TDecider.Initialize` |

### Load

```csharp
public static async ValueTask<AggregateRunner<...>> Load(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters,
    CancellationToken ct = default)
```

Loads an existing aggregate by replaying all events. Calls `Initialize(parameters)` then folds all events through `Transition`.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `store` | `EventStore<TEvent>` | Event store to load from |
| `streamId` | `string` | Stream to replay |
| `parameters` | `TParameters` | Passed to `TDecider.Initialize` |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** An aggregate runner with state reconstructed from events. Version equals the last event's `SequenceNumber`.

## Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `StreamId` | `string` | The stream identifier this aggregate is bound to |
| `Parameters` | `TParameters` | Initialization parameters passed to the decider |
| `State` | `TState` | Current aggregate state |
| `Version` | `long` | Current version (event count, `0` if no events) |
| `Effects` | `IReadOnlyList<TEffect>` | Accumulated effects from all handled commands |
| `IsTerminal` | `bool` | `TDecider.IsTerminal(State)` |

## Methods

### Handle

```csharp
public async ValueTask<Result<TState, TError>> Handle(
    TCommand command, CancellationToken ct = default)
```

Processes a command through the full cycle: decide → persist → transition.

1. Acquires the internal `SemaphoreSlim` (serializes concurrent calls)
2. Calls `TDecider.Decide(State, command)`
3. If `Ok`: appends events via `EventStore.AppendAsync`
4. Folds events through `TDecider.Transition` to update state and collect effects
5. Returns the new state

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `command` | `TCommand` | Command to process |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** `Result<TState, TError>` — new state on success, error on domain rejection.

**Throws:** `ConcurrencyException` if another process wrote to the same stream.

**Thread safety:** Safe to call concurrently — internal `SemaphoreSlim` serializes access.

### Rebuild

```csharp
public async ValueTask<TState> Rebuild(CancellationToken ct = default)
```

Reloads state from the event store. Re-initializes and replays all events. Clears accumulated effects.

### Dispose

```csharp
public void Dispose()
```

Releases the internal `SemaphoreSlim`.

## OpenTelemetry

All operations create `Activity` spans via `EventSourcingDiagnostics.Source` (`"Picea.Glauca"`).

| Operation | Activity Name |
| --------- | ------------- |
| `Handle` | `"Handle"` |
| `Load` | `"Load"` |
| `Rebuild` | `"Rebuild"` |

## Example

```csharp
var store = new InMemoryEventStore<CounterEvent>();

// Create
using var runner = AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);

// Handle
var result = await runner.Handle(new CounterCommand.Add(5));
// result.IsOk → true, runner.State.Count → 5, runner.Version → 5

// Load
using var loaded = await AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Load(store, "counter-1", default);
// loaded.State.Count → 5
```

## See Also

- [AggregateRunner Concept](../concepts/aggregate-runner.md)
- [Tutorial 01: First Aggregate](../tutorials/01-first-aggregate.md)
- [ResolvingAggregateRunner](resolving-aggregate-runner.md)
