# Sagas

## What Is a Saga?

A saga is a **long-running process** that coordinates actions across multiple aggregates by reacting to domain events and producing commands (effects). It's modeled as a [Mealy machine](https://en.wikipedia.org/wiki/Mealy_machine) — the same automaton abstraction that powers the Picea kernel.

The key difference between an aggregate and a saga:

| Concern | Aggregate | Saga |
| ------- | --------- | ---- |
| Input | Commands (imperative) | Events (reactive) |
| Output | Events (facts) | Effects/Commands (instructions) |
| Purpose | Enforce invariants | Coordinate workflows |
| Identity | `Decide(state, command)` | `Transition(state, event)` |

A saga **reacts** — it does not **decide**.

## The Saga Interface

```csharp
public interface Saga<TState, TEvent, TEffect, TParameters>
    : Automaton<TState, TEvent, TEffect, TParameters>
{
    static virtual bool IsTerminal(TState state) => false;
}
```

A saga extends the Picea `Automaton` interface and adds `IsTerminal` — a predicate that identifies when the saga has completed its work.

## Example: Order Fulfillment

A 3-step order fulfillment saga:

```text
AwaitingPayment ──[PaymentReceived]──▶ Shipping ──[OrderShipped]──▶ Completed
        │                                  │
        └──[OrderCancelled]──▶ Cancelled ◀─┘
```

```csharp
using Picea;
using Picea.Glauca.Saga;

public enum OrderSagaState
{
    AwaitingPayment,
    Shipping,
    Completed,
    Cancelled
}

public class OrderFulfillment
    : Saga<OrderSagaState, OrderDomainEvent, FulfillmentCommand, Unit>
{
    public static (OrderSagaState, FulfillmentCommand) Initialize(Unit _) =>
        (OrderSagaState.AwaitingPayment, new FulfillmentCommand.None());

    public static (OrderSagaState, FulfillmentCommand) Transition(
        OrderSagaState state, OrderDomainEvent @event) =>
        (state, @event) switch
        {
            (OrderSagaState.AwaitingPayment, OrderDomainEvent.PaymentReceived e) =>
                (OrderSagaState.Shipping,
                    new FulfillmentCommand.ShipOrder(e.OrderId)),

            (OrderSagaState.Shipping, OrderDomainEvent.OrderShipped e) =>
                (OrderSagaState.Completed,
                    new FulfillmentCommand.SendConfirmation(e.OrderId, e.TrackingNumber)),

            (OrderSagaState.Shipping, OrderDomainEvent.OrderCancelled e) =>
                (OrderSagaState.Cancelled,
                    new FulfillmentCommand.RefundPayment(e.OrderId, 0)),

            (OrderSagaState.AwaitingPayment, OrderDomainEvent.OrderCancelled _) =>
                (OrderSagaState.Cancelled, new FulfillmentCommand.None()),

            _ => (state, new FulfillmentCommand.None())
        };

    public static bool IsTerminal(OrderSagaState state) =>
        state is OrderSagaState.Completed or OrderSagaState.Cancelled;
}
```

## The SagaRunner

The `SagaRunner` handles persistence and lifecycle:

```csharp
var store = new InMemoryEventStore<OrderDomainEvent>();

using var saga = SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Create(store, "order-123", default);

// Handle an event → get the effect to dispatch
var effect = await saga.Handle(
    new OrderDomainEvent.PaymentReceived("order-123", 99.99m));
// effect is FulfillmentCommand.ShipOrder("order-123")

// Dispatch the effect to the shipping aggregate...
```

### SagaRunner Behavior

| Behavior | Description |
| -------- | ----------- |
| **Event persistence** | Events are only persisted if the state actually changed |
| **Terminal state** | Once terminal, further events are ignored and the init effect is returned |
| **Thread safety** | Concurrent `Handle` calls are serialized via `SemaphoreSlim` |
| **Effect accumulation** | All produced effects are accumulated in `runner.Effects` |
| **Tracing** | Spans emitted on `Picea.Glauca.Saga` ActivitySource |

### Terminal States

Once a saga reaches a terminal state, it's done:

```csharp
// After order is completed...
Assert.True(saga.IsTerminal);

// Further events are ignored — returns the init effect
var effect = await saga.Handle(
    new OrderDomainEvent.PaymentReceived("order-123", 50.00m));
// effect is FulfillmentCommand.None() — the saga is done
```

This prevents completed sagas from reacting to stale or replayed events.

## Design Notes

### Sagas Are Not Aggregates

A saga does not enforce invariants. It coordinates. The effects it produces are *requests*, not guarantees — the target aggregate still validates via its own `Decide`.

### Compensation

When a saga reaches a cancelled state, it produces compensation commands (e.g., `RefundPayment`). The saga doesn't execute the refund — it tells the payment aggregate to do so. If the refund fails, that's a new event the saga can react to.

### Sagas Are Event-Sourced Too

The saga's own transitions are persisted as events. This means:
- Sagas survive process restarts (reload from the stream)
- Saga state at any point in time can be reconstructed
- The saga's history is fully auditable

## See Also

- [Tutorial 04: Sagas](../tutorials/04-sagas.md) — step-by-step walkthrough
- [SagaRunner Reference](../reference/saga-runner.md) — complete API documentation
- [Event Sourcing](event-sourcing.md) — the foundational pattern
