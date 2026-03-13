# KurrentDBEventStore\<TEvent\>

Production `EventStore<TEvent>` implementation backed by KurrentDB (EventStoreDB).

## Class Definition

```csharp
public sealed class KurrentDBEventStore<TEvent>(
    KurrentDBClient client,
    Func<TEvent, (string EventType, ReadOnlyMemory<byte> Data)> serialize,
    Func<string, ReadOnlyMemory<byte>, TEvent> deserialize)
    : EventStore<TEvent>
```

**Namespace:** `Picea.Glauca.KurrentDB`
**Assembly:** `Picea.Glauca.KurrentDB`
**Implements:** `Picea.Glauca.EventStore<TEvent>`

## Constructor Parameters

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `client` | `KurrentDBClient` | KurrentDB client instance |
| `serialize` | `Func<TEvent, (string, ReadOnlyMemory<byte>)>` | Converts an event to a type name and binary payload |
| `deserialize` | `Func<string, ReadOnlyMemory<byte>, TEvent>` | Converts a type name and binary payload back to an event |

## Methods

### AppendAsync

```csharp
public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
    string streamId, TEvent[] events, long expectedVersion,
    CancellationToken ct = default)
```

Appends events to a KurrentDB stream.

**Version mapping:**

- `expectedVersion = 0` → `StreamState.NoStream` (new stream)
- `expectedVersion = N` → `StreamRevision(N - 1)` (0-based KurrentDB revision)

**Exception mapping:**

- `WrongExpectedVersionException` → `ConcurrencyException`

**Tracing:** Creates an `Activity` named `"KurrentDB.Append"` via `EventSourcingDiagnostics.Source`.

### LoadAsync (full)

```csharp
public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
    string streamId, CancellationToken ct = default)
```

Reads the entire stream from KurrentDB.

**Returns:** Events ordered by position. Returns empty list if the stream doesn't exist (`StreamNotFound` is caught).

**Sequence number mapping:** KurrentDB 0-based `EventNumber` is mapped to 1-based `StoredEvent.SequenceNumber` (`EventNumber + 1`).

**Tracing:** Creates an `Activity` named `"KurrentDB.Load"`.

### LoadAsync (incremental)

```csharp
public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
    string streamId, long afterVersion,
    CancellationToken ct = default)
```

Reads events from a KurrentDB stream starting after the given version.

**Position mapping:** `afterVersion` (1-based) is converted to a KurrentDB `StreamPosition` for the read start.

**Tracing:** Creates an `Activity` named `"KurrentDB.LoadAfter"`.

## Serialization

The delegate-based design keeps serialization out of the event store. You provide two functions:

```csharp
// Serialize: event → (type name, binary data)
(string EventType, ReadOnlyMemory<byte> Data) Serialize(MyEvent e) =>
    (e.GetType().Name, JsonSerializer.SerializeToUtf8Bytes<object>(e));

// Deserialize: (type name, binary data) → event
MyEvent Deserialize(string type, ReadOnlyMemory<byte> data) =>
    type switch
    {
        "Created" => JsonSerializer.Deserialize<MyEvent.Created>(data.Span),
        "Updated" => JsonSerializer.Deserialize<MyEvent.Updated>(data.Span),
        _ => throw new InvalidOperationException($"Unknown event type: {type}")
    };
```

This supports any serialization format: JSON, MessagePack, Protobuf, etc.

## Version Mapping Table

| Glauca Concept | KurrentDB Equivalent |
| -------------- | -------------------- |
| `expectedVersion = 0` | `StreamState.NoStream` |
| `expectedVersion = N` | `StreamRevision(N - 1)` |
| `SequenceNumber = 1` | `EventNumber = 0` (first event) |
| `SequenceNumber = N` | `EventNumber = N - 1` |
| `ConcurrencyException` | Mapped from `WrongExpectedVersionException` |

## Dependencies

- `KurrentDB.Client` (>= 1.3.0)
- `Picea.Glauca`

## Example

```csharp
using KurrentDB.Client;
using Picea.Glauca.KurrentDB;

var client = new KurrentDBClient(
    KurrentDBClientSettings.Create("esdb://localhost:2113?tls=false"));

var store = new KurrentDBEventStore<CounterEvent>(
    client,
    e => (e.GetType().Name, JsonSerializer.SerializeToUtf8Bytes<object>(e)),
    (type, data) => type switch
    {
        "Incremented" => JsonSerializer.Deserialize<CounterEvent.Incremented>(data.Span),
        "Decremented" => JsonSerializer.Deserialize<CounterEvent.Decremented>(data.Span),
        _ => throw new InvalidOperationException($"Unknown: {type}")
    });

// Use exactly like InMemoryEventStore
using var runner = AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);
```

## See Also

- [KurrentDB Setup Guide](../guides/kurrentdb-setup.md)
- [EventStore Interface](event-store.md)
- [Implementing Event Stores](../guides/implementing-event-stores.md)
