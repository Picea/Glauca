# The Aggregate Runner

## What Is an Aggregate Runner?

The `AggregateRunner` is the runtime that bridges a pure [Picea `Decider`](https://github.com/Picea/Picea) and an `EventStore`. It handles:

- **State reconstruction** — Loads events from the store and replays them through `Transition`
- **Command handling** — Validates via `Decide`, persists via `AppendAsync`, transitions in-memory
- **Optimistic concurrency** — Tracks the stream version and enforces it on every append
- **Thread safety** — Serializes concurrent `Handle` calls via `SemaphoreSlim`
- **Lifecycle** — Implements `IDisposable` for clean resource management

## The Execution Model

```text
Create/Load
    │
    ▼
┌──────────────────────────────────────────────┐
│ AggregateRunner                              │
│                                              │
│  State: TState          (in-memory)          │
│  Version: long          (last event seq#)    │
│  Effects: List<TEffect> (accumulated)        │
│                                              │
│  Handle(command) ────────────────────────┐    │
│    │                                    │    │
│    ├─ 1. gate.WaitAsync()  (serialize)  │    │
│    ├─ 2. Decide(state, command)         │    │
│    │      └─ Err? → return error        │    │
│    │      └─ Ok([]) → return state      │    │
│    ├─ 3. store.AppendAsync(events, ver) │    │
│    │      └─ Version mismatch? → throw  │    │
│    └─ 4. Transition(state, each event)  │    │
│           └─ update state + version     │    │
│                                              │
└──────────────────────────────────────────────┘
```

## Create vs. Load

There are two ways to obtain a runner:

### Create — New Aggregate

```csharp
using var runner = AggregateRunner<MyDecider, MyState,
    MyCommand, MyEvent, MyEffect, MyError, Unit>
    .Create(store, "stream-1", parameters: default);
```

This initializes the aggregate at its initial state with version `0`. No events are loaded from the store. Use this when you know the stream is new.

### Load — Existing Aggregate

```csharp
using var runner = await AggregateRunner<MyDecider, MyState,
    MyCommand, MyEvent, MyEffect, MyError, Unit>
    .Load(store, "stream-1", parameters: default);
```

This loads all events from the stream and replays them to reconstruct the current state. Use this when the stream may already exist.

## Command Handling

The `Handle` method is the primary entry point:

```csharp
Result<TState, TError> result = await runner.Handle(command);

if (result.IsOk)
    Console.WriteLine($"New state: {result.Value}");
else
    Console.WriteLine($"Error: {result.Error}");
```

The return type is `Result<TState, TError>`:
- **Ok** — The command was accepted. Contains the new state after applying all events.
- **Err** — The command was rejected by the `Decide` function. Contains the domain error.

Note that `ConcurrencyException` is thrown as an exception (not returned as `Err`) because it's an infrastructure concern, not a domain concern.

## Rebuild

The `Rebuild` method reloads all events from the store and reconstructs state from scratch:

```csharp
var state = await runner.Rebuild();
```

This is useful when the in-memory state may be stale — for example, if another process appended events to the same stream.

## Thread Safety

The `SemaphoreSlim` gate ensures that concurrent `Handle` calls are serialized:

```csharp
// These can be called from multiple threads safely
var task1 = runner.Handle(new MyCommand.DoSomething());
var task2 = runner.Handle(new MyCommand.DoSomethingElse());

// They will execute one at a time, in arrival order
await Task.WhenAll(task1, task2);
```

The gate also protects `Rebuild`, so you can safely call it while `Handle` calls are in flight.

## Terminal State

The `IsTerminal` property delegates to `TDecider.IsTerminal(_state)`:

```csharp
if (runner.IsTerminal)
    Console.WriteLine("Aggregate has reached a terminal state");
```

The default implementation on `Decider` returns `false` (no terminal state). Override it in your decider to define which states are terminal.

## Effects

The runner accumulates effects from all handled commands:

```csharp
foreach (var effect in runner.Effects)
{
    // Dispatch effects to external systems
    await effectHandler.HandleAsync(effect);
}
```

Effects are produced by `Transition` alongside state changes. They represent side effects that the domain requests but cannot execute (e.g., send an email, publish a message).

## See Also

- [ResolvingAggregateRunner](../reference/resolving-aggregate-runner.md) — automatic conflict resolution
- [Event Sourcing](event-sourcing.md) — the foundational pattern
- [AggregateRunner Reference](../reference/aggregate-runner.md) — complete API documentation
