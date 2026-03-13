# Picea.Glauca

Event Sourcing patterns modeled as Mealy machine automata — Aggregate runners, Saga orchestration, Projections, and pluggable EventStore adapters. Built on the [Picea](https://github.com/Picea/Picea) kernel.

## Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| `Picea.Glauca` | [![NuGet](https://img.shields.io/nuget/v/Picea.Glauca)](https://www.nuget.org/packages/Picea.Glauca) | Core: `AggregateRunner`, `ResolvingAggregateRunner`, `SagaRunner`, `Projection`, `EventStore`, `InMemoryEventStore` |
| `Picea.Glauca.KurrentDB` | [![NuGet](https://img.shields.io/nuget/v/Picea.Glauca.KurrentDB)](https://www.nuget.org/packages/Picea.Glauca.KurrentDB) | [KurrentDB](https://www.kurrent.io/) adapter for `EventStore<TEvent>` |

## Installation

```bash
dotnet add package Picea.Glauca
```

For KurrentDB persistence:

```bash
dotnet add package Picea.Glauca.KurrentDB
```

## Quick Start

Picea.Glauca turns a [Picea `Decider`](https://github.com/Picea/Picea) into a fully persistent, concurrency-safe event-sourced aggregate. Define your domain logic once as a pure decider — the runner handles persistence, replay, and optimistic concurrency.

### 1. Define a Decider

```csharp
using Picea;
using Picea.Glauca;

public readonly record struct CounterState(int Count);

public interface CounterCommand
{
    record struct Add(int Amount) : CounterCommand;
}

public interface CounterEvent
{
    record struct Incremented : CounterEvent;
    record struct Decremented : CounterEvent;
}

public interface CounterEffect
{
    record struct None : CounterEffect;
}

public interface CounterError
{
    record struct Overflow(int Current, int Amount, int Max) : CounterError;
    record struct Underflow(int Current, int Amount) : CounterError;
}

public class CounterDecider
    : Decider<CounterState, CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
{
    public const int MaxCount = 100;

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
}
```

### 2. Run with an AggregateRunner

```csharp
// Create a store (InMemoryEventStore for testing, KurrentDBEventStore for production)
var store = new InMemoryEventStore<CounterEvent>();

// Create a new aggregate
using var counter = AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, streamId: "counter-1", parameters: default);

// Handle commands — returns Result<TState, TError>
var result = await counter.Handle(new CounterCommand.Add(5));
// result.IsOk == true, counter.State.Count == 5

// Load an existing aggregate from the stream
using var loaded = await AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Load(store, streamId: "counter-1", parameters: default);
// loaded.State.Count == 5, loaded.Version == 5
```

## Projections

Build read models by folding over event streams. Projections support full replay and incremental catch-up.

```csharp
var projection = new Projection<CounterEvent, int>(
    initial: 0,
    apply: (count, @event) => @event switch
    {
        CounterEvent.Incremented => count + 1,
        CounterEvent.Decremented => count - 1,
        _ => count
    });

// Full replay from the beginning
var total = await projection.Project(store, "counter-1");

// Incremental catch-up (only processes new events since last read)
var updated = await projection.CatchUp(store, "counter-1");
```

## Conflict Resolution

`ResolvingAggregateRunner` extends `AggregateRunner` with automatic optimistic concurrency resolution. When a `ConcurrencyException` occurs, the runner loads the conflicting events and delegates to a `ConflictResolver` to attempt automatic merge — up to 3 retries.

```csharp
public class CounterDecider
    : ConflictResolver<CounterState, CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
{
    // ... Initialize, Decide, Transition as before ...

    public static Result<CounterEvent[], ConflictNotResolved> ResolveConflicts(
        CounterState currentState,
        CounterState projectedState,
        CounterEvent[] ourEvents,
        IReadOnlyList<CounterEvent> theirEvents)
    {
        // Increments/decrements are commutative — safe to replay
        // Just validate the merged result stays within bounds
        return projectedState.Count switch
        {
            > MaxCount => Result<CounterEvent[], ConflictNotResolved>.Err(
                new ConflictNotResolved("Would exceed maximum")),
            < 0 => Result<CounterEvent[], ConflictNotResolved>.Err(
                new ConflictNotResolved("Would go below zero")),
            _ => Result<CounterEvent[], ConflictNotResolved>.Ok(ourEvents)
        };
    }
}

// Use with ResolvingAggregateRunner instead of AggregateRunner
using var counter = ResolvingAggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);
```

## Sagas

Long-running processes modeled as Mealy machines. Sagas react to domain events and produce commands for other aggregates. They support terminal states — once terminal, further events are ignored.

```csharp
using Picea.Glauca.Saga;

public class OrderFulfillment
    : Saga<OrderSagaState, OrderDomainEvent, FulfillmentCommand, Unit>
{
    public static (OrderSagaState, FulfillmentCommand) Initialize(Unit _) =>
        (OrderSagaState.AwaitingPayment, new FulfillmentCommand.None());

    public static (OrderSagaState, FulfillmentCommand) Transition(
        OrderSagaState state, OrderDomainEvent @event) =>
        (state, @event) switch
        {
            (OrderSagaState.AwaitingPayment, OrderDomainEvent.PaymentReceived e) =>
                (OrderSagaState.Shipping, new FulfillmentCommand.ShipOrder(e.OrderId)),

            (OrderSagaState.Shipping, OrderDomainEvent.OrderShipped e) =>
                (OrderSagaState.Completed,
                    new FulfillmentCommand.SendConfirmation(e.OrderId, e.TrackingNumber)),

            _ => (state, new FulfillmentCommand.None())
        };

    public static bool IsTerminal(OrderSagaState state) =>
        state is OrderSagaState.Completed or OrderSagaState.Cancelled;
}

// Run with SagaRunner
using var saga = SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Create(store, "order-123", default);

var effect = await saga.Handle(
    new OrderDomainEvent.PaymentReceived("order-123", 99.99m));
// effect is FulfillmentCommand.ShipOrder — dispatch to shipping aggregate
```

## KurrentDB Adapter

For production persistence, use `KurrentDBEventStore` with delegate-based serialization — no framework coupling.

```csharp
using Picea.Glauca.KurrentDB;
using KurrentDB.Client;

var client = new KurrentDBClient(settings);

var store = new KurrentDBEventStore<MyEvent>(
    client,
    serialize: e => (e.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(e, options)),
    deserialize: (type, data) =>
        (MyEvent)JsonSerializer.Deserialize(data.Span, typeMap[type], options)!);
```

The adapter handles version mapping (1-based sequence numbers ↔ KurrentDB's 0-based revisions) and maps KurrentDB's `WrongExpectedVersionException` to `ConcurrencyException`.

## EventStore Interface

Implement `EventStore<TEvent>` to plug in any persistence backend:

```csharp
public interface EventStore<TEvent>
{
    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
        string streamId, TEvent[] events, long expectedVersion,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, CancellationToken ct = default);

    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, long afterVersion,
        CancellationToken ct = default);
}
```

`InMemoryEventStore<TEvent>` is included for unit testing.

## OpenTelemetry

The runners emit distributed tracing spans via `System.Diagnostics.ActivitySource` — zero external dependencies, compatible with any OpenTelemetry collector.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Picea.Glauca")
        .AddSource("Picea.Glauca.Saga"));
```

## API Reference

| Type | Description |
|------|-------------|
| `EventStore<TEvent>` | Async event persistence with optimistic concurrency |
| `StoredEvent<TEvent>` | Event envelope: `SequenceNumber`, `Event`, `Timestamp` |
| `ConcurrencyException` | Thrown on version mismatch |
| `InMemoryEventStore<TEvent>` | Thread-safe in-memory store for testing |
| `AggregateRunner<...>` | Event-sourced aggregate with persistence and concurrency control |
| `ResolvingAggregateRunner<...>` | Aggregate runner with automatic conflict resolution |
| `ConflictResolver<...>` | Decider that can resolve concurrency conflicts |
| `ConflictNotResolved` | Resolution failure marker |
| `Projection<TEvent, TReadModel>` | Read model builder via fold (full replay + catch-up) |
| `Saga<TState, TEvent, TEffect, TParameters>` | Automaton with terminal state support |
| `SagaRunner<...>` | Event-sourced saga runtime |
| `KurrentDBEventStore<TEvent>` | KurrentDB adapter with delegate-based serialization |

## The Picea Ecosystem

| Package | Purpose | Repository |
|---------|---------|------------|
| [Picea](https://www.nuget.org/packages/Picea) | Core kernel, runtime, Decider, Result, diagnostics | [picea/picea](https://github.com/Picea/Picea) |
| [Picea.Abies](https://www.nuget.org/packages/Picea.Abies) | MVU framework for Blazor | [picea/abies](https://github.com/Picea/Abies) |
| **Picea.Glauca** | Event Sourcing (this package) | [picea/glauca](https://github.com/Picea/Glauca) |

## License

Apache-2.0
