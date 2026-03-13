# Implementing Event Stores

How to implement the `EventStore<TEvent>` interface for a custom storage backend.

## The Interface

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

Three operations, one exception type (`ConcurrencyException`), one wrapper type (`StoredEvent<TEvent>`). All methods return `ValueTask` and accept `CancellationToken`.

## Contract Requirements

Your implementation must satisfy these invariants:

### 1. Optimistic Concurrency

`AppendAsync` must check that the stream's current version matches `expectedVersion` before appending. If it doesn't match, throw `ConcurrencyException`:

```csharp
if (currentVersion != expectedVersion)
    throw new ConcurrencyException(streamId, expectedVersion, currentVersion);
```

Version semantics (count-based):

- A stream that doesn't exist yet has version `0` (zero events)
- Each event increments the version by 1
- After appending 3 events to a new stream, the version is `3`

### 2. Sequence Numbers

`StoredEvent.SequenceNumber` must be a 1-based, monotonically increasing, gap-free sequence within the stream:

```csharp
// First event:  SequenceNumber = 1
// Second event: SequenceNumber = 2
// Third event:  SequenceNumber = 3
```

### 3. LoadAsync (full)

`LoadAsync(streamId)` returns all events in the stream, ordered by sequence number. Returns an empty list for non-existent streams.

### 4. LoadAsync (after version)

`LoadAsync(streamId, afterVersion)` returns only events with `SequenceNumber > afterVersion`. This powers incremental catch-up in projections.

### 5. Timestamps

Each `StoredEvent` must include a `DateTimeOffset Timestamp` representing when the event was persisted.

## Implementation Guide

### Step 1: Create the Project

```bash
mkdir Picea.Glauca.MyStore
cd Picea.Glauca.MyStore
dotnet new classlib
dotnet add reference ../Picea.Glauca/Picea.Glauca.csproj
```

### Step 2: Implement the Interface

```csharp
using Picea.Glauca;

public class MyEventStore<TEvent> : EventStore<TEvent>
{
    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
        string streamId, TEvent[] events, long expectedVersion,
        CancellationToken ct = default)
    {
        // 1. Begin transaction
        // 2. Read current version (count of events)
        // 3. If currentVersion != expectedVersion, throw ConcurrencyException
        // 4. Assign sequence numbers starting at currentVersion + 1
        // 5. Persist events with sequence numbers and timestamps
        // 6. Commit transaction
        // 7. Return stored events
    }

    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, CancellationToken ct = default)
    {
        // Return all events ordered by sequence number
        // Return empty list if stream doesn't exist
    }

    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, long afterVersion,
        CancellationToken ct = default)
    {
        // Return events WHERE SequenceNumber > afterVersion
        // Ordered by sequence number
    }
}
```

### Step 3: Handle Serialization

For storage backends that don't natively store .NET objects (most of them), you need a serialization strategy. The `KurrentDBEventStore` uses delegate-based serialization:

```csharp
public class MyEventStore<TEvent>(
    MyClient client,
    Func<TEvent, (string EventType, byte[] Data)> serialize,
    Func<string, ReadOnlyMemory<byte>, TEvent> deserialize)
    : EventStore<TEvent>
{
    // Use serialize/deserialize in AppendAsync and LoadAsync
}
```

This keeps serialization concerns out of the event store implementation and lets users choose their serializer (System.Text.Json, MessagePack, Protobuf, etc.).

## Reference Implementation

See `InMemoryEventStore<TEvent>` for a complete, thread-safe reference implementation:

```csharp
// Key patterns to follow:
// - Thread safety via Lock (new .NET Lock type)
// - Count-based version tracking per stream (version = event count)
// - 1-based sequence numbers
// - ConcurrencyException on version mismatch
// - Empty list for non-existent streams
```

See `KurrentDBEventStore<TEvent>` for a production implementation with:

- Delegate-based serialization
- Version mapping (Glauca count-based → KurrentDB 0-based revisions)
- Activity tracing via `ActivitySource`

## Testing Your Implementation

Use the same test patterns as `InMemoryEventStoreTests` and `KurrentDBEventStoreTests`:

```csharp
[Fact]
public async Task Append_and_load_round_trips()
{
    await using var store = CreateStore();
    var events = new[] { CreateTestEvent() };

    await store.AppendAsync("stream-1", events, 0);
    var loaded = await store.LoadAsync("stream-1");

    Assert.Single(loaded);
    Assert.Equal(1, loaded[0].SequenceNumber);
    Assert.Equal(events[0], loaded[0].Event);
}

[Fact]
public async Task Append_with_wrong_version_throws_ConcurrencyException()
{
    await using var store = CreateStore();
    await store.AppendAsync("stream-1", [CreateTestEvent()], 0);

    await Assert.ThrowsAsync<ConcurrencyException>(
        () => store.AppendAsync("stream-1", [CreateTestEvent()], 0).AsTask());
}

[Fact]
public async Task Load_after_version_returns_only_newer_events()
{
    await using var store = CreateStore();
    await store.AppendAsync("stream-1", [CreateTestEvent(), CreateTestEvent()], 0);
    await store.AppendAsync("stream-1", [CreateTestEvent()], 2);

    var events = await store.LoadAsync("stream-1", 2);

    Assert.Single(events);
    Assert.Equal(3, events[0].SequenceNumber);
}

[Fact]
public async Task Load_nonexistent_stream_returns_empty()
{
    await using var store = CreateStore();
    var events = await store.LoadAsync("nonexistent");
    Assert.Empty(events);
}
```

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Use KurrentDB | [KurrentDB Setup](kurrentdb-setup.md) |
| See the full API | [EventStore Reference](../reference/event-store.md) |
