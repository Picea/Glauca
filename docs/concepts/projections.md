# Projections

## What Is a Projection?

A projection builds a **read model** by folding over an event stream. It takes an initial value and an `apply` function, then processes each event in sequence to produce the current read model state.

Mathematically, a projection is a [catamorphism](https://en.wikipedia.org/wiki/Catamorphism) — a fold:

$$\text{readModel} = \text{fold}(\text{apply}, \text{initial}, \text{events})$$

This is the same structure as the aggregate's state reconstruction, but with a different target type. The aggregate folds events into *its own state*. A projection folds events into *any read model*.

## Why Projections?

In event-sourced systems, the write side (aggregates) and the read side (queries) have different needs:

| Write Side | Read Side |
| ---------- | --------- |
| Optimized for consistency | Optimized for query patterns |
| One event stream per aggregate | Multiple views per stream |
| Normalized (events are atomic facts) | Denormalized (pre-computed for fast reads) |

Projections bridge this gap. The same event stream can power:

- A **summary view** (total count)
- A **detailed view** (full event history with timestamps)
- An **analytics pipeline** (aggregated metrics)
- A **search index** (full-text searchable)

## How Glauca Projections Work

### Full Replay

`Project` loads all events from a stream and applies them from the beginning:

```csharp
var projection = new Projection<OrderEvent, OrderSummary>(
    initial: new OrderSummary(0, 0m),
    apply: (summary, @event) => @event switch
    {
        OrderEvent.ItemAdded e =>
            summary with
            {
                ItemCount = summary.ItemCount + 1,
                Total = summary.Total + e.Price
            },
        OrderEvent.ItemRemoved e =>
            summary with
            {
                ItemCount = summary.ItemCount - 1,
                Total = summary.Total - e.Price
            },
        _ => summary
    });

OrderSummary result = await projection.Project(store, "order-42");
```

### Incremental Catch-Up

`CatchUp` processes only events that arrived since the last time the projection ran:

```csharp
// First run — processes all events
await projection.Project(store, "order-42");

// ... time passes, new events are appended ...

// Second run — only processes new events since last read
await projection.CatchUp(store, "order-42");
```

Internally, the projection tracks `LastProcessedVersion` and uses `LoadAsync(streamId, afterVersion)` to fetch only new events.

### Inline Apply

For real-time use cases, you can apply events directly as they happen:

```csharp
// Apply a raw event (no version tracking)
projection.Apply(new OrderEvent.ItemAdded("widget", 9.99m));

// Apply a stored event (updates LastProcessedVersion)
projection.Apply(new StoredEvent<OrderEvent>(42, someEvent, DateTimeOffset.UtcNow));
```

## Multiple Projections, One Stream

Different projections can fold the same stream into different shapes:

```csharp
// Projection 1: Total count
var countProjection = new Projection<OrderEvent, int>(
    initial: 0,
    apply: (count, e) => e is OrderEvent.ItemAdded ? count + 1 : count);

// Projection 2: Revenue
var revenueProjection = new Projection<OrderEvent, decimal>(
    initial: 0m,
    apply: (total, e) => e switch
    {
        OrderEvent.ItemAdded a => total + a.Price,
        OrderEvent.ItemRemoved r => total - r.Price,
        _ => total
    });

// Projection 3: Event log
var logProjection = new Projection<OrderEvent, List<string>>(
    initial: [],
    apply: (log, e) => [..log, e.ToString()]);

// All three fold the same stream
await countProjection.Project(store, "order-42");
await revenueProjection.Project(store, "order-42");
await logProjection.Project(store, "order-42");
```

## Design Notes

### Projections Are Not Aggregates

A projection is a **read model** — it doesn't produce events or enforce invariants. It's a one-way transformation from events to a queryable shape.

| Concern | Aggregate | Projection |
| ------- | --------- | ---------- |
| Direction | Command → Event | Event → Read Model |
| Consistency | Strong (optimistic concurrency) | Eventual (catches up) |
| Side effects | Produces effects | None |
| Thread safety | SemaphoreSlim gate | Caller's responsibility |

### Eventual Consistency

Projections are eventually consistent with the event stream. Between a `Handle` call on the aggregate and a `CatchUp` call on the projection, the read model may be stale. This is by design — it follows the CQRS separation of concerns.

## See Also

- [Event Sourcing](event-sourcing.md) — the foundational pattern
- [Projection Patterns Guide](../guides/projection-patterns.md) — recipes for common projection patterns
- [Projection Reference](../reference/projection.md) — complete API documentation
