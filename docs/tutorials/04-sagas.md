# Tutorial 04: Sagas

Build an order fulfillment workflow that coordinates payment, shipping, and notification.

## What You'll Learn

- Define a `Saga` as a Mealy machine
- Use `SagaRunner` to persist saga state and produce effects
- Handle terminal states (completed, cancelled)
- Implement compensation on cancellation

## Prerequisites

Complete [Tutorial 01: First Aggregate](01-first-aggregate.md).

## Step 1: Design the Workflow

```text
AwaitingPayment ──[PaymentReceived]──▶ Shipping ──[OrderShipped]──▶ Completed ✓
        │                                  │
        └──[OrderCancelled]──▶ Cancelled ◀─┘
                                    ✓
```

The saga reacts to events from other aggregates and produces commands that drive the next step.

## Step 2: Define the Types

```csharp
using Picea;
using Picea.Glauca.Saga;

// Saga state
public enum OrderSagaState
{
    AwaitingPayment,
    Shipping,
    Completed,
    Cancelled
}

// Events the saga reacts to (from other aggregates)
public interface OrderDomainEvent
{
    record struct PaymentReceived(string OrderId, decimal Amount) : OrderDomainEvent;
    record struct OrderShipped(string OrderId, string TrackingNumber) : OrderDomainEvent;
    record struct OrderCancelled(string OrderId, string Reason) : OrderDomainEvent;
}

// Commands the saga produces (for other aggregates)
public interface FulfillmentCommand
{
    record struct None : FulfillmentCommand;
    record struct ShipOrder(string OrderId) : FulfillmentCommand;
    record struct SendConfirmation(string OrderId, string TrackingNumber) : FulfillmentCommand;
    record struct RefundPayment(string OrderId, decimal Amount) : FulfillmentCommand;
}
```

## Step 3: Implement the Saga

```csharp
public class OrderFulfillment
    : Saga<OrderSagaState, OrderDomainEvent, FulfillmentCommand, Unit>
{
    public static (OrderSagaState, FulfillmentCommand) Initialize(Unit _) =>
        (OrderSagaState.AwaitingPayment, new FulfillmentCommand.None());

    public static (OrderSagaState, FulfillmentCommand) Transition(
        OrderSagaState state, OrderDomainEvent @event) =>
        (state, @event) switch
        {
            // Happy path: payment → ship → confirm
            (OrderSagaState.AwaitingPayment, OrderDomainEvent.PaymentReceived e) =>
                (OrderSagaState.Shipping,
                    new FulfillmentCommand.ShipOrder(e.OrderId)),

            (OrderSagaState.Shipping, OrderDomainEvent.OrderShipped e) =>
                (OrderSagaState.Completed,
                    new FulfillmentCommand.SendConfirmation(e.OrderId, e.TrackingNumber)),

            // Cancellation with compensation
            (OrderSagaState.Shipping, OrderDomainEvent.OrderCancelled e) =>
                (OrderSagaState.Cancelled,
                    new FulfillmentCommand.RefundPayment(e.OrderId, 0)),

            (OrderSagaState.AwaitingPayment, OrderDomainEvent.OrderCancelled _) =>
                (OrderSagaState.Cancelled, new FulfillmentCommand.None()),

            // Ignore unexpected events
            _ => (state, new FulfillmentCommand.None())
        };

    public static bool IsTerminal(OrderSagaState state) =>
        state is OrderSagaState.Completed or OrderSagaState.Cancelled;
}
```

Key points:
- `Transition` is a pure function — no I/O
- Each transition produces an **effect** (a command for another aggregate)
- `IsTerminal` defines when the saga is done
- The catch-all `_` ignores events that don't apply in the current state

## Step 4: Run with SagaRunner

```csharp
var store = new InMemoryEventStore<OrderDomainEvent>();

using var saga = SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Create(store, "order-123", default);

Console.WriteLine($"State: {saga.State}"); // AwaitingPayment
```

## Step 5: Process Events

```csharp
// Payment arrives — saga moves to Shipping and commands ShipOrder
var effect1 = await saga.Handle(
    new OrderDomainEvent.PaymentReceived("order-123", 99.99m));

Console.WriteLine($"State: {saga.State}");    // Shipping
Console.WriteLine($"Effect: {effect1}");      // ShipOrder { OrderId = order-123 }

// Ship order effect should be dispatched to the shipping aggregate...
// Then the shipping aggregate emits OrderShipped

// Order shipped — saga moves to Completed and commands SendConfirmation
var effect2 = await saga.Handle(
    new OrderDomainEvent.OrderShipped("order-123", "TRACK-456"));

Console.WriteLine($"State: {saga.State}");    // Completed
Console.WriteLine($"Terminal: {saga.IsTerminal}"); // True
Console.WriteLine($"Effect: {effect2}");      // SendConfirmation { ... }
```

## Step 6: Terminal State Protection

Once terminal, the saga ignores further events:

```csharp
// Saga is completed — further events are ignored
var effect3 = await saga.Handle(
    new OrderDomainEvent.PaymentReceived("order-123", 50.00m));

Console.WriteLine($"State: {saga.State}");    // Still Completed
Console.WriteLine($"Effect: {effect3}");      // None (init effect)
```

## Step 7: Cancellation with Compensation

```csharp
var store2 = new InMemoryEventStore<OrderDomainEvent>();

using var saga2 = SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Create(store2, "order-456", default);

// Payment received → Shipping
await saga2.Handle(new OrderDomainEvent.PaymentReceived("order-456", 75.00m));

// Order cancelled while shipping → Cancelled + RefundPayment
var cancelEffect = await saga2.Handle(
    new OrderDomainEvent.OrderCancelled("order-456", "Customer request"));

Console.WriteLine($"State: {saga2.State}");      // Cancelled
Console.WriteLine($"Effect: {cancelEffect}");     // RefundPayment { ... }
Console.WriteLine($"Terminal: {saga2.IsTerminal}"); // True
```

The saga doesn't execute the refund — it produces a `RefundPayment` command. Your application dispatches that command to the payment aggregate.

## Step 8: Reload a Saga

Like aggregates, sagas are event-sourced and can be reloaded:

```csharp
// Reload the saga from its event stream
using var reloaded = await SagaRunner<OrderFulfillment, OrderSagaState,
    OrderDomainEvent, FulfillmentCommand, Unit>
    .Load(store, "order-123", default);

Console.WriteLine($"State: {reloaded.State}");     // Completed
Console.WriteLine($"Version: {reloaded.Version}");  // 2
```

## What You Built

```text
OrderDomainEvent ─── Transition ───▶ (NewState, FulfillmentCommand)
                        │                          │
                        │ pure                     │ dispatch to
                        │                          │ other aggregates
                   SagaRunner                      │
                   (persistence +                  ▼
                    terminal check)          Shipping, Payment,
                                            Notification aggregates
```

## What's Next

| If you want to… | Read |
| ---------------- | ---- |
| Understand saga theory | [Sagas Concept](../concepts/sagas.md) |
| Use KurrentDB in production | [KurrentDB Setup Guide](../guides/kurrentdb-setup.md) |
| Test event sourcing code | [Testing Strategies Guide](../guides/testing-strategies.md) |
