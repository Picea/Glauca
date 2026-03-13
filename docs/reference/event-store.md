# EventStore\<TEvent\>

The core persistence interface for event streams.

## Interface Definition

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

**Namespace:** `Picea.Glauca`
**Assembly:** `Picea.Glauca`

## Methods

### AppendAsync

```csharp
ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
    string streamId, TEvent[] events, long expectedVersion,
    CancellationToken ct = default)
```

Appends events to a stream with optimistic concurrency control.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `streamId` | `string` | Unique identifier for the event stream |
| `events` | `TEvent[]` | Events to append |
| `expectedVersion` | `long` | Expected current version (event count) of the stream. `0` for new streams. |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** The stored events with assigned sequence numbers and timestamps.

**Throws:** `ConcurrencyException` when the stream's actual version doesn't match `expectedVersion`.

**Version semantics (count-based):**
- New stream: `expectedVersion = 0`
- After appending 1 event: version is `1`
- After appending N events: version is `N`

### LoadAsync (full)

```csharp
ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
    string streamId, CancellationToken ct = default)
```

Loads all events from a stream.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `streamId` | `string` | Stream to load |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** All events in order, wrapped in `StoredEvent<TEvent>`. Empty list if the stream doesn't exist.

### LoadAsync (incremental)

```csharp
ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
    string streamId, long afterVersion,
    CancellationToken ct = default)
```

Loads events after a specific version. Used by `Projection.CatchUp`.

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `streamId` | `string` | Stream to load |
| `afterVersion` | `long` | Only return events with `SequenceNumber > afterVersion` |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** Events after the specified version.

---

## StoredEvent\<TEvent\>

```csharp
public readonly record struct StoredEvent<TEvent>(
    long SequenceNumber,
    TEvent Event,
    DateTimeOffset Timestamp);
```

**Namespace:** `Picea.Glauca`

Wraps an event with persistence metadata.

| Property | Type | Description |
| -------- | ---- | ----------- |
| `SequenceNumber` | `long` | 1-based position within the stream |
| `Event` | `TEvent` | The domain event |
| `Timestamp` | `DateTimeOffset` | When the event was persisted |

---

## ConcurrencyException

```csharp
public class ConcurrencyException(
    string streamId,
    long expectedVersion,
    long actualVersion) : Exception
{
    public string StreamId { get; } = streamId;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}
```

**Namespace:** `Picea.Glauca`
**Inherits:** `Exception`

Thrown when `AppendAsync` detects a version mismatch.

| Property | Type | Description |
| -------- | ---- | ----------- |
| `StreamId` | `string` | The stream where the conflict occurred |
| `ExpectedVersion` | `long` | The version the caller expected |
| `ActualVersion` | `long` | The actual current version |

## Implementations

| Implementation | Package | Description |
| -------------- | ------- | ----------- |
| `InMemoryEventStore<TEvent>` | `Picea.Glauca` | Thread-safe (`Lock`), dictionary-backed. For testing. Includes `GetStream(streamId)` helper. |
| `KurrentDBEventStore<TEvent>` | `Picea.Glauca.KurrentDB` | Production adapter for KurrentDB. Delegate-based serialization. |

## See Also

- [Implementing Event Stores](../guides/implementing-event-stores.md)
- [Event Sourcing Concept](../concepts/event-sourcing.md)
- [KurrentDB Reference](kurrentdb.md)
