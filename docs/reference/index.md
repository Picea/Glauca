# API Reference

Complete API documentation for all public types in Picea.Glauca and Picea.Glauca.KurrentDB.

## Core Types

| Type | Namespace | Description |
| ---- | --------- | ----------- |
| [`EventStore<TEvent>`](event-store.md) | `Picea.Glauca` | Event persistence interface |
| [`StoredEvent<TEvent>`](event-store.md#storedevent) | `Picea.Glauca` | Event wrapper with metadata |
| [`ConcurrencyException`](event-store.md#concurrencyexception) | `Picea.Glauca` | Optimistic concurrency violation |
| [`AggregateRunner`](aggregate-runner.md) | `Picea.Glauca` | Event-sourced aggregate runtime |
| [`ResolvingAggregateRunner`](resolving-aggregate-runner.md) | `Picea.Glauca` | Aggregate runtime with conflict resolution |
| [`ConflictResolver`](conflict-resolver.md) | `Picea.Glauca` | Conflict resolution interface |
| [`ConflictNotResolved`](conflict-resolver.md#conflictnotresolved) | `Picea.Glauca` | Unresolvable conflict marker |
| [`Projection<TEvent, TReadModel>`](projection.md) | `Picea.Glauca` | Read model projection |
| [`Saga<TState, TEvent, TEffect, TParameters>`](saga-runner.md#saga-interface) | `Picea.Glauca.Saga` | Saga interface |
| [`SagaRunner`](saga-runner.md) | `Picea.Glauca.Saga` | Event-sourced saga runtime |
| [`EventSourcingDiagnostics`](diagnostics.md) | `Picea.Glauca` | OpenTelemetry tracing |

## Extension Types

| Type | Package | Description |
| ---- | ------- | ----------- |
| [`KurrentDBEventStore<TEvent>`](kurrentdb.md) | `Picea.Glauca.KurrentDB` | KurrentDB event store adapter |

## Type Parameter Conventions

All runners use the same type parameter naming:

| Parameter | Constraint | Meaning |
| --------- | ---------- | ------- |
| `TDecider` | `Decider<...>` | The decider (domain logic) |
| `TState` | — | Aggregate state type |
| `TCommand` | — | Command type |
| `TEvent` | — | Event type |
| `TEffect` | — | Side-effect type |
| `TError` | — | Error type |
| `TParameters` | — | Initialization parameters |

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Understand the concepts | [Concepts](../concepts/index.md) |
| Walk through examples | [Tutorials](../tutorials/README.md) |
| Solve a specific problem | [How-To Guides](../guides/index.md) |
