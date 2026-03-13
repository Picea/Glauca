# Getting Started

Get up and running with Picea.Glauca in 5 minutes.

## Quick Start

### 1. Install

```bash
dotnet add package Picea.Glauca
```

### 2. Define a Decider

Your domain logic is a pure [Picea `Decider`](https://github.com/Picea/Picea) — no I/O, no side effects, no dependencies:

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

### 3. Run with an AggregateRunner

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

### 4. Build a Read Model

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
```

### 5. Test It (No Runner Needed)

```csharp
// Decide is a pure function — test it directly
var decision = CounterDecider.Decide(new CounterState(5), new CounterCommand.Add(3));
Assert.True(decision.IsOk);
Assert.Equal(3, decision.Value.Length);

// Transition is a pure function — test it directly
var (state, _) = CounterDecider.Transition(new CounterState(5), new CounterEvent.Incremented());
Assert.Equal(6, state.Count);
```

## What Just Happened?

1. You defined **state**, **commands**, **events**, **effects**, and **errors** as simple C# types
2. You wrote **pure functions** — `Decide` validates commands, `Transition` applies events
3. The **AggregateRunner** handled persistence, replay, optimistic concurrency, and lifecycle
4. The **Projection** built a read model by folding over the event stream
5. You tested the domain logic **directly** — no runner, no async, no mocking

## What's Next

| If you want to… | Read |
| ---------------- | ---- |
| Understand event sourcing theory | [Event Sourcing](../concepts/event-sourcing.md) |
| Build a complete aggregate | [Tutorial 01: First Aggregate](../tutorials/01-first-aggregate.md) |
| Add conflict resolution | [Conflict Resolution](../concepts/conflict-resolution.md) |
| Coordinate long-running processes | [Sagas](../concepts/sagas.md) |
| Use KurrentDB in production | [KurrentDB Setup Guide](../guides/kurrentdb-setup.md) |
| See the full API | [Reference](../reference/index.md) |
