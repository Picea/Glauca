# Projection\<TEvent, TReadModel\>

Read model builder via event stream folding.

## Class Definition

```csharp
public class Projection<TEvent, TReadModel>(
    TReadModel initial,
    Func<TReadModel, TEvent, TReadModel> apply)
```

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`

## Constructor Parameters

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `initial` | `TReadModel` | Starting value for the read model |
| `apply` | `Func<TReadModel, TEvent, TReadModel>` | Fold function: `(currentModel, event) → newModel` |

## Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `ReadModel` | `TReadModel` | Current projected state |
| `LastProcessedVersion` | `long` | Sequence number of the last processed `StoredEvent` |

## Methods

### Project

```csharp
public async ValueTask<TReadModel> Project(
    EventStore<TEvent> store, string streamId,
    CancellationToken ct = default)
```

Full replay: loads all events from the stream and folds them through `apply`.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `store` | `EventStore<TEvent>` | Event store to read from |
| `streamId` | `string` | Stream to project |
| `ct` | `CancellationToken` | Cancellation token (optional) |

**Returns:** The projected read model.

### CatchUp

```csharp
public async ValueTask<TReadModel> CatchUp(
    EventStore<TEvent> store, string streamId,
    CancellationToken ct = default)
```

Incremental projection: loads only events after `LastProcessedVersion` and folds them. Does **not** reset the read model.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `store` | `EventStore<TEvent>` | Event store to read from |
| `streamId` | `string` | Stream to catch up on |
| `ct` | `CancellationToken` | Cancellation token (optional) |

**Returns:** The updated read model.

### Apply (raw event)

```csharp
public void Apply(TEvent @event)
```

Applies a single event to the read model inline. Does **not** update `LastProcessedVersion`.

### Apply (stored event)

```csharp
public void Apply(StoredEvent<TEvent> storedEvent)
```

Applies a stored event to the read model and updates `LastProcessedVersion` to the event's `SequenceNumber`.

## Example

```csharp
var store = new InMemoryEventStore<CounterEvent>();

var projection = new Projection<CounterEvent, int>(
    initial: 0,
    apply: (count, e) => e switch
    {
        CounterEvent.Incremented => count + 1,
        CounterEvent.Decremented => count - 1,
        _ => count
    });

// Full replay
await projection.Project(store, "counter-1");
Console.WriteLine(projection.ReadModel); // e.g. 5

// Later — only process new events
await projection.CatchUp(store, "counter-1");
```

## See Also

- [Projections Concept](../concepts/projections.md)
- [Tutorial 02: Projections](../tutorials/02-projections.md)
- [Projection Patterns](../guides/projection-patterns.md)
