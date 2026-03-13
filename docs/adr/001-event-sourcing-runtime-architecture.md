# ADR-001: Event Sourcing Runtime Architecture

## Status

Accepted

## Date

2026-01-15

## Context

Picea.Glauca provides an event sourcing runtime on top of the [Picea kernel](https://github.com/Picea/Picea). The kernel defines pure algebraic abstractions (`Decider`, `Automaton`) with no persistence or lifecycle concerns. We need a runtime layer that:

1. Persists events to a pluggable event store
2. Enforces optimistic concurrency via version checking
3. Serializes concurrent access within a single process
4. Supports read-model projections from event streams
5. Supports long-running sagas (Mealy machines) with terminal states
6. Provides domain-level conflict resolution as an alternative to hard concurrency failures

## Decision

### Runner Pattern

We use **sealed, generic runner classes** (`AggregateRunner`, `ResolvingAggregateRunner`, `SagaRunner`) that own the lifecycle of a single aggregate or saga instance. Runners are:

- **Generic over the decider/saga type** — the domain logic is provided via static interface members on the type parameter, not via constructor injection. This follows the Picea kernel's use of C# static virtual members.
- **Factory-method constructed** — `Create()` for new instances, `Load()` for rehydration. No public constructors.
- **Internally synchronized** — A `SemaphoreSlim` gate serializes `Handle` calls within a process. Cross-process concurrency is handled by the event store via `ConcurrencyException`.
- **Disposable** — `IDisposable` to release the semaphore.

### Event Store Abstraction

A minimal `EventStore<TEvent>` interface with three operations:

- `AppendAsync(streamId, expectedVersion, events)` — optimistic append
- `LoadAsync(streamId)` — full stream read
- `LoadAsync(streamId, afterVersion)` — incremental read

Implementations are separate packages (e.g., `Picea.Glauca.KurrentDB`). `InMemoryEventStore` is included for testing.

### Conflict Resolution

`ConflictResolver<...>` extends `Decider<...>` with a `ResolveConflicts` method. `ResolvingAggregateRunner` catches `ConcurrencyException`, loads conflicting events, and delegates to the domain for resolution. Up to 3 retries.

### Projections

`Projection<TEvent, TReadModel>` is a simple fold wrapper with `Project` (full replay) and `CatchUp` (incremental). Not a runner — it's a utility class.

### Diagnostics

`System.Diagnostics.ActivitySource` for OpenTelemetry-compatible tracing. Two sources: `"Picea.Glauca"` (aggregates, event stores) and `"Picea.Glauca.Saga"` (sagas).

## Alternatives Considered

### 1. Abstract Base Classes Instead of Generic Runners

Rejected: would force inheritance hierarchies on user code. The generic + static interface approach keeps domain logic in pure POCOs with no base class requirement.

### 2. Dependency Injection for Decider

Rejected: the kernel uses static virtual members, not instance methods. Runners follow the same pattern for consistency. DI is used at the composition root (registering event stores), not inside the runner.

### 3. Built-in Snapshotting

Deferred: snapshotting is an optimization that adds significant complexity (snapshot stores, version tracking, conditional loading). The replay-from-zero approach is correct and sufficient for streams under ~10k events. Snapshotting can be added as a separate package later.

### 4. Event Upcasting in the Event Store

Deferred: schema evolution (upcasting old event formats to new ones) is handled in serialization delegates, not in the event store interface. This keeps the interface minimal and gives users full control over their migration strategy.

## Consequences

### Positive

- Minimal surface area — 3 runner types, 1 store interface, 1 projection utility
- Domain logic remains pure (no framework base classes)
- Pluggable storage via a 3-method interface
- Built-in conflict resolution at the domain level
- OpenTelemetry tracing with zero configuration

### Negative

- No built-in snapshotting (users must implement their own for large streams)
- No built-in subscription/projection hosting (users build their own catch-up loops)
- 7 type parameters on runners can be verbose (mitigated by `using` aliases in C# 12+)

### Risks

- The static virtual interface pattern is relatively new in C# — some tooling (serializers, DI containers) may not support it well yet.
- Without snapshotting, performance degrades linearly with stream length. This is acceptable for the 0.x release but must be addressed before 1.0.
