# SagaRunner & Saga Interface

Event-sourced saga runtime and the `Saga` interface.

## Saga Interface

```csharp
public interface Saga<TState, TEvent, TEffect, TParameters>
    : Automaton<TState, TEvent, TEffect, TParameters>
{
    static virtual bool IsTerminal(TState state) => false;
}
```

**Namespace:** `Picea.Glauca.Saga`
**Assembly:** `Picea.Glauca`
**Extends:** `Picea.Automaton<TState, TEvent, TEffect, TParameters>`

### Required Static Members

| Member | Signature | Description |
| ------ | --------- | ----------- |
| `Initialize` | `(TState, TEffect) Initialize(TParameters)` | Initial state and effect |
| `Transition` | `(TState, TEffect) Transition(TState, TEvent)` | State machine transition (from `Automaton`) |

### Optional Static Members

| Member | Signature | Default | Description |
| ------ | --------- | ------- | ----------- |
| `IsTerminal` | `bool IsTerminal(TState)` | `false` | Whether the saga has reached a final state |

---

## SagaRunner

```csharp
public sealed class SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>
    : IDisposable
    where TSaga : Saga<TState, TEvent, TEffect, TParameters>
```

**Namespace:** `Picea.Glauca.Saga`
**Assembly:** `Picea.Glauca`
**Implements:** `IDisposable`

## Type Parameters

| Parameter | Constraint | Description |
| --------- | ---------- | ----------- |
| `TSaga` | `Saga<...>` | Saga implementing the state machine |
| `TState` | — | Saga state |
| `TEvent` | — | Event type (inputs the saga reacts to) |
| `TEffect` | — | Effect type (commands the saga produces) |
| `TParameters` | — | Initialization parameters |

## Factory Methods

### Create

```csharp
public static SagaRunner<...> Create(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters)
```

Creates a new saga instance at its initial state.

### Load

```csharp
public static async ValueTask<SagaRunner<...>> Load(
    EventStore<TEvent> store,
    string streamId,
    TParameters parameters,
    CancellationToken ct = default)
```

Loads a saga by replaying events from the store.

## Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `StreamId` | `string` | The stream identifier this saga is bound to |
| `Parameters` | `TParameters` | Initialization parameters |
| `State` | `TState` | Current saga state |
| `Version` | `long` | Current version (event count) |
| `Effects` | `IReadOnlyList<TEffect>` | Accumulated effects from all handled events |
| `IsTerminal` | `bool` | Cached result of `TSaga.IsTerminal(State)` |

## Methods

### Handle

```csharp
public async ValueTask<TEffect> Handle(TEvent @event, CancellationToken ct = default)
```

Processes an event through the saga:

1. If `IsTerminal` is true: returns the initial effect (no-op), **does not persist**
2. Calls `TSaga.Transition(State, @event)`
3. If state changed: appends the event via `EventStore.AppendAsync`
4. Updates state
5. Returns the produced effect

| Parameter | Type | Description |
| --------- | ---- | ----------- |
| `@event` | `TEvent` | The event to process |
| `ct` | `CancellationToken` | Cancellation token |

**Returns:** `TEffect` — the command/effect produced by the transition.

**Key behaviours:**

- **Terminal guard:** Events are silently ignored once the saga reaches a terminal state
- **Idempotent transitions:** If `Transition` produces the same state, the event is **not persisted** (state didn't change)
- **Thread safety:** Internal `SemaphoreSlim` serializes concurrent calls

### Dispose

```csharp
public void Dispose()
```

Releases the internal `SemaphoreSlim`.

## OpenTelemetry

Operations create `Activity` spans via `SagaDiagnostics.Source` (`"Picea.Glauca.Saga"`).

| Operation | Activity Name |
| --------- | ------------- |
| `Handle` | `"Handle"` |
| `Load` | `"Load"` |
| `Rebuild` | `"Rebuild"` |

## Example

```csharp
var store = new InMemoryEventStore<OrderDomainEvent>();

using var saga = SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Create(store, "order-123", default);

// Process events — saga produces commands
var cmd1 = await saga.Handle(new OrderDomainEvent.PaymentReceived("order-123", 99.99m));
// cmd1 = ShipOrder { OrderId = "order-123" }
// saga.State = Shipping

var cmd2 = await saga.Handle(new OrderDomainEvent.OrderShipped("order-123", "TRACK-456"));
// cmd2 = SendConfirmation { ... }
// saga.State = Completed, saga.IsTerminal = true

// Further events are ignored
var cmd3 = await saga.Handle(new OrderDomainEvent.PaymentReceived("order-123", 50m));
// cmd3 = None (initial effect), state unchanged
```

## See Also

- [Sagas Concept](../concepts/sagas.md)
- [Tutorial 04: Sagas](../tutorials/04-sagas.md)
