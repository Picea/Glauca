# KurrentDB Setup

How to use KurrentDB (EventStoreDB) as your production event store.

## Prerequisites

- .NET 10.0 SDK
- Docker (for local development)
- `Picea.Glauca.KurrentDB` package

```bash
dotnet add package Picea.Glauca.KurrentDB
```

## Step 1: Start KurrentDB Locally

```bash
docker run -d --name kurrentdb \
  -p 2113:2113 \
  -e EVENTSTORE_INSECURE=true \
  -e EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true \
  eventstore/eventstore:latest
```

Verify it's running:

```bash
curl -s http://localhost:2113/health/live
# Expected: {"status":"ok"}
```

## Step 2: Create the Event Store

```csharp
using KurrentDB.Client;
using Picea.Glauca;
using Picea.Glauca.KurrentDB;
using System.Text.Json;

// 1. Create the KurrentDB client
var settings = KurrentDBClientSettings.Create("esdb://localhost:2113?tls=false");
var client = new KurrentDBClient(settings);

// 2. Define serialization delegates
(string EventType, ReadOnlyMemory<byte> Data) Serialize(CounterEvent e) =>
    (e.GetType().Name, JsonSerializer.SerializeToUtf8Bytes<object>(e));

CounterEvent Deserialize(string type, ReadOnlyMemory<byte> data) =>
    type switch
    {
        nameof(CounterEvent.Incremented) =>
            JsonSerializer.Deserialize<CounterEvent.Incremented>(data.Span),
        nameof(CounterEvent.Decremented) =>
            JsonSerializer.Deserialize<CounterEvent.Decremented>(data.Span),
        _ => throw new InvalidOperationException($"Unknown event type: {type}")
    };

// 3. Create the event store
var store = new KurrentDBEventStore<CounterEvent>(client, Serialize, Deserialize);
```

## Step 3: Use with AggregateRunner

Once you have the store, usage is identical to `InMemoryEventStore`:

```csharp
using var counter = AggregateRunner<CounterDecider, CounterState,
    CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
    .Create(store, "counter-1", default);

await counter.Handle(new CounterCommand.Add(5));
Console.WriteLine($"Count: {counter.State.Count}"); // 5
```

## Serialization Strategies

The delegate-based design lets you choose your serializer:

### System.Text.Json (shown above)

Simple, no dependencies, good for most cases.

### MessagePack

```csharp
(string EventType, ReadOnlyMemory<byte> Data) Serialize(CounterEvent e) =>
    (e.GetType().Name, MessagePackSerializer.Serialize<object>(e));

CounterEvent Deserialize(string type, ReadOnlyMemory<byte> data) =>
    type switch
    {
        "Incremented" => MessagePackSerializer.Deserialize<CounterEvent.Incremented>(data),
        "Decremented" => MessagePackSerializer.Deserialize<CounterEvent.Decremented>(data),
        _ => throw new InvalidOperationException($"Unknown event type: {type}")
    };
```

### Versioned Event Types

For schema evolution, include version info in the event type:

```csharp
(string EventType, ReadOnlyMemory<byte> Data) Serialize(OrderEvent e) => e switch
{
    OrderEvent.PlacedV2 p => ("OrderPlaced-v2", JsonSerializer.SerializeToUtf8Bytes(p)),
    OrderEvent.PlacedV1 p => ("OrderPlaced-v1", JsonSerializer.SerializeToUtf8Bytes(p)),
    _ => (e.GetType().Name, JsonSerializer.SerializeToUtf8Bytes<object>(e))
};

OrderEvent Deserialize(string type, ReadOnlyMemory<byte> data) => type switch
{
    "OrderPlaced-v2" => JsonSerializer.Deserialize<OrderEvent.PlacedV2>(data.Span),
    "OrderPlaced-v1" => UpcastV1(JsonSerializer.Deserialize<OrderEvent.PlacedV1>(data.Span)),
    _ => throw new InvalidOperationException($"Unknown: {type}")
};
```

## Version Mapping

KurrentDB uses 0-based stream positions internally. Glauca uses 0-based versions. The `KurrentDBEventStore` handles the mapping:

| Glauca Concept | KurrentDB Equivalent |
| -------------- | -------------------- |
| `expectedVersion = 0` (new stream) | `StreamState.NoStream` |
| `expectedVersion = N` (N > 0) | `StreamRevision(N - 1)` |
| `SequenceNumber` (1-based) | Stream position (0-based) + 1 |
| `ConcurrencyException` | Mapped from `WrongExpectedVersionException` |

## Observability

`KurrentDBEventStore` creates `Activity` spans for all operations using the `Picea.Glauca` `ActivitySource`:

```csharp
// Configure OpenTelemetry to capture Picea.Glauca traces
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Picea.Glauca")
        .AddSource("Picea.Glauca.Saga")
        .AddOtlpExporter());
```

## Production Configuration

### Connection Strings

```csharp
// Single node (development)
var settings = KurrentDBClientSettings.Create("esdb://localhost:2113?tls=false");

// Cluster (production)
var settings = KurrentDBClientSettings.Create(
    "esdb://node1:2113,node2:2113,node3:2113?tls=true&tlsVerifyCert=true");

// With authentication
var settings = KurrentDBClientSettings.Create(
    "esdb://admin:changeit@node1:2113?tls=true");
```

### Connection Pooling

The `KurrentDBClient` manages connections internally. Create one instance and reuse it across your application (register as singleton in DI).

### Error Handling

The event store maps KurrentDB exceptions to Glauca exceptions:

| KurrentDB Exception | Glauca Exception |
| -------------------- | ----------------- |
| `WrongExpectedVersionException` | `ConcurrencyException` |
| Other exceptions | Propagated as-is |

## Docker Compose for Development

```yaml
services:
  kurrentdb:
    image: eventstore/eventstore:latest
    ports:
      - "2113:2113"
    environment:
      - EVENTSTORE_INSECURE=true
      - EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true
      - EVENTSTORE_MEM_DB=true
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:2113/health/live"]
      interval: 5s
      timeout: 3s
      retries: 5
```

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Implement your own event store | [Implementing Event Stores](implementing-event-stores.md) |
| See the full API | [KurrentDB Reference](../reference/kurrentdb.md) |
