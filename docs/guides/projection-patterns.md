# Projection Patterns

Advanced techniques for building read models with `Projection<TEvent, TReadModel>`.

## Pattern 1: Multiple Projections per Stream

A single event stream can power many read models. Each projection is an independent fold:

```csharp
var store = new InMemoryEventStore<OrderEvent>();

// Projection 1: Total revenue
var revenue = new Projection<OrderEvent, decimal>(
    initial: 0m,
    apply: (total, e) => e switch
    {
        OrderEvent.Placed(_, var amount) => total + amount,
        OrderEvent.Refunded(_, var amount) => total - amount,
        _ => total
    });

// Projection 2: Order status map
var statuses = new Projection<OrderEvent, Dictionary<string, string>>(
    initial: [],
    apply: (map, e) =>
    {
        switch (e)
        {
            case OrderEvent.Placed(var id, _):
                map[id] = "Placed"; break;
            case OrderEvent.Shipped(var id, _):
                map[id] = "Shipped"; break;
            case OrderEvent.Refunded(var id, _):
                map[id] = "Refunded"; break;
        }
        return map;
    });
```

## Pattern 2: Catch-Up Loop

For long-running read model processes, poll the event store periodically:

```csharp
var projection = new Projection<OrderEvent, DashboardModel>(
    initial: new DashboardModel(),
    apply: (model, e) => model.Apply(e));

// Initial full replay
await projection.Project(store, streamId);

// Catch-up loop
var cts = new CancellationTokenSource();
while (!cts.Token.IsCancellationRequested)
{
    await projection.CatchUp(store, streamId);
    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
}
```

`CatchUp` calls `LoadAsync(streamId, afterVersion)` internally — only fetches new events.

## Pattern 3: Real-Time Inline Apply

When you need instant updates (e.g., in a handler pipeline), apply events directly:

```csharp
// After handling a command, apply the new events immediately
var result = await runner.Handle(command);
if (result.IsOk)
{
    foreach (var @event in result.Value)
        projection.Apply(@event);
    // ReadModel is now up-to-date without a store round-trip
}
```

Note: `Apply(TEvent)` does not update `LastProcessedVersion` — use `Apply(StoredEvent<TEvent>)` if version tracking matters.

## Pattern 4: Snapshot Projections

For streams with many events, project in chunks and persist snapshots:

```csharp
// Load snapshot from your snapshot store
var snapshot = await snapshotStore.LoadAsync<DashboardModel>(streamId);

var projection = new Projection<OrderEvent, DashboardModel>(
    initial: snapshot?.Model ?? new DashboardModel(),
    apply: (model, e) => model.Apply(e));

// If we have a snapshot, catch up from that point
if (snapshot is not null)
    await projection.CatchUp(store, streamId);
else
    await projection.Project(store, streamId);

// Periodically save snapshot
await snapshotStore.SaveAsync(streamId, projection.ReadModel, projection.LastProcessedVersion);
```

Picea.Glauca doesn't include a snapshot store — it's a concern of your application layer.

## Pattern 5: Cross-Stream Projections

Build a read model across multiple streams:

```csharp
var globalRevenue = new Projection<OrderEvent, decimal>(
    initial: 0m,
    apply: (total, e) => e switch
    {
        OrderEvent.Placed(_, var amount) => total + amount,
        OrderEvent.Refunded(_, var amount) => total - amount,
        _ => total
    });

// Project multiple streams into the same read model
foreach (var streamId in await GetAllOrderStreamIds())
{
    await globalRevenue.Project(store, streamId);
}
```

**Important:** Cross-stream projections require iterating over streams. For large-scale use, consider a subscription-based approach at the event store level.

## Pattern 6: Projection Composition

Compose multiple projections into a single pass:

```csharp
// Composite read model
public record DashboardModel(decimal Revenue, int OrderCount, int RefundCount);

var dashboard = new Projection<OrderEvent, DashboardModel>(
    initial: new DashboardModel(0m, 0, 0),
    apply: (model, e) => e switch
    {
        OrderEvent.Placed(_, var amount) =>
            model with { Revenue = model.Revenue + amount, OrderCount = model.OrderCount + 1 },
        OrderEvent.Refunded(_, var amount) =>
            model with { Revenue = model.Revenue - amount, RefundCount = model.RefundCount + 1 },
        _ => model
    });

// Single pass, single read model, multiple metrics
await dashboard.Project(store, streamId);
```

## Choosing a Pattern

| Scenario | Pattern |
| -------- | ------- |
| Different views of the same data | Multiple Projections |
| Background process building a read model | Catch-Up Loop |
| Immediate consistency after command | Inline Apply |
| Long event streams (>10k events) | Snapshot Projections |
| Reporting across all streams | Cross-Stream |
| Multiple metrics in one read | Composition |

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Understand projection theory | [Projections Concept](../concepts/projections.md) |
| Walk through a projection tutorial | [Tutorial 02: Projections](../tutorials/02-projections.md) |
| See the API | [Projection Reference](../reference/projection.md) |
