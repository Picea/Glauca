# Tutorial 02: Projections

Build multiple read models from a single event stream.

## What You'll Learn

- Create projections with custom fold functions
- Use full replay vs. incremental catch-up
- Build multiple views from the same stream

## Prerequisites

Complete [Tutorial 01: First Aggregate](01-first-aggregate.md) or have a working `Decider` and `EventStore`.

## Step 1: Set Up the Event Stream

We'll use the counter domain from the tests for a clear example:

```csharp
using Picea;
using Picea.Glauca;

var store = new InMemoryEventStore<CounterEvent>();

using var counter = AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);

// Generate some events
await counter.Handle(new CounterCommand.Add(3));  // 3 Incremented events
await counter.Handle(new CounterCommand.Add(-1)); // 1 Decremented event
await counter.Handle(new CounterCommand.Add(2));  // 2 Incremented events
// Stream now has 6 events: I, I, I, D, I, I
```

## Step 2: Create a Simple Projection

A projection is a fold function wrapped in a `Projection<TEvent, TReadModel>`:

```csharp
// Read model: net count
var countProjection = new Projection<CounterEvent, int>(
    initial: 0,
    apply: (count, @event) => @event switch
    {
        CounterEvent.Incremented => count + 1,
        CounterEvent.Decremented => count - 1,
        _ => count
    });

// Full replay
var total = await countProjection.Project(store, "counter-1");
Console.WriteLine($"Count: {total}"); // Count: 4
```

## Step 3: Multiple Read Models

The same event stream can power completely different read models:

```csharp
// Read model 1: Just the count
var countProjection = new Projection<CounterEvent, int>(
    initial: 0,
    apply: (count, e) => e switch
    {
        CounterEvent.Incremented => count + 1,
        CounterEvent.Decremented => count - 1,
        _ => count
    });

// Read model 2: Event type statistics
var statsProjection = new Projection<CounterEvent, Dictionary<string, int>>(
    initial: new Dictionary<string, int>(),
    apply: (stats, e) =>
    {
        var name = e.GetType().Name;
        stats[name] = stats.GetValueOrDefault(name) + 1;
        return stats;
    });

// Read model 3: History (audit log)
var historyProjection = new Projection<CounterEvent, List<string>>(
    initial: [],
    apply: (log, e) => [..log, $"{DateTime.UtcNow:HH:mm:ss} — {e.GetType().Name}"]);

// All three process the same stream
await countProjection.Project(store, "counter-1");
await statsProjection.Project(store, "counter-1");
await historyProjection.Project(store, "counter-1");

Console.WriteLine($"Count: {countProjection.ReadModel}");
// Count: 4

Console.WriteLine($"Stats: {string.Join(", ", statsProjection.ReadModel.Select(kv => $"{kv.Key}={kv.Value}"))}");
// Stats: Incremented=5, Decremented=1

Console.WriteLine($"History entries: {historyProjection.ReadModel.Count}");
// History entries: 6
```

## Step 4: Incremental Catch-Up

After the initial projection, use `CatchUp` to process only new events:

```csharp
// Initial projection
await countProjection.Project(store, "counter-1");
Console.WriteLine($"Count: {countProjection.ReadModel}"); // 4
Console.WriteLine($"Version: {countProjection.LastProcessedVersion}"); // 6

// More events arrive...
await counter.Handle(new CounterCommand.Add(3));

// Catch up — only processes the 3 new events
await countProjection.CatchUp(store, "counter-1");
Console.WriteLine($"Count: {countProjection.ReadModel}"); // 7
Console.WriteLine($"Version: {countProjection.LastProcessedVersion}"); // 9
```

`CatchUp` uses `LoadAsync(streamId, afterVersion)` internally — it only fetches events after `LastProcessedVersion`.

## Step 5: Inline Apply

For real-time scenarios where you're already handling events (e.g., in an observer), apply them directly:

```csharp
// Apply a raw event
countProjection.Apply(new CounterEvent.Incremented());
// ReadModel updated, but LastProcessedVersion unchanged

// Apply a stored event (updates version tracking)
countProjection.Apply(new StoredEvent<CounterEvent>(
    10, new CounterEvent.Incremented(), DateTimeOffset.UtcNow));
// Both ReadModel and LastProcessedVersion updated
```

## What You Built

```text
Event Stream (counter-1)
    │
    ├──▶ Projection → int (count)
    │
    ├──▶ Projection → Dictionary<string, int> (stats)
    │
    └──▶ Projection → List<string> (history)

Each is an independent fold over the same events.
```

## What's Next

| If you want to… | Read |
| ---------------- | ---- |
| Handle concurrent writes | [Tutorial 03: Conflict Resolution](03-conflict-resolution.md) |
| Coordinate across aggregates | [Tutorial 04: Sagas](04-sagas.md) |
| See advanced projection patterns | [Projection Patterns Guide](../guides/projection-patterns.md) |
