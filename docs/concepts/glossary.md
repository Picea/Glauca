# Glossary

Definitions of terms used throughout the Picea.Glauca documentation.

## A

### Aggregate
A consistency boundary in the domain — a cluster of entities and value objects that are treated as a single unit for data changes. In Glauca, an aggregate is a `Decider` backed by an `AggregateRunner`.

### AggregateRunner
The runtime that bridges a pure `Decider` and an `EventStore`. Handles state reconstruction, command handling, optimistic concurrency, and thread safety.

### Append
Adding events to an event stream. Append-only — events are never modified or deleted.

## C

### Catch-Up
Processing only events that arrived since the last time a projection ran. Uses `LoadAsync(streamId, afterVersion)`.

### Command
An imperative instruction to change the system state. Validated by the `Decide` function before producing events. Represented as a discriminated union (C# interface with record struct members).

### Commutativity
The property that two operations produce the same result regardless of order: $f(g(x)) = g(f(x))$. Commutative operations can be automatically resolved during concurrency conflicts.

### ConcurrencyException
Thrown when an optimistic concurrency check fails during event appending. Contains `StreamId`, `ExpectedVersion`, and `ActualVersion`.

### ConflictResolver
A `Decider` that implements `ResolveConflicts` — a static method that examines concurrent events and determines whether they can be automatically merged.

## D

### Decider
A Picea kernel concept: a state machine with command validation. Provides `Initialize`, `Decide`, `Transition`, and `IsTerminal`. The pure domain logic — no I/O, no side effects.

## E

### Effect
A side effect requested by the domain logic. Produced by `Transition` alongside state changes. Effects are data — the runner or application code is responsible for executing them.

### Event
An immutable fact that something happened. Produced by `Decide` when a command is accepted. Applied by `Transition` to produce new state. Events are the source of truth in event sourcing.

### EventStore
The persistence abstraction: `AppendAsync` and `LoadAsync` with optimistic concurrency control. Implementations include `InMemoryEventStore` (testing) and `KurrentDBEventStore` (production).

### Event Stream
An ordered, append-only sequence of events identified by a `streamId`. Each stream represents one aggregate instance.

## F

### Fold
A mathematical operation that processes a sequence element by element, accumulating a result. Event sourcing state reconstruction is a left fold: `fold(transition, initial, events)`.

## I

### InMemoryEventStore
Thread-safe in-memory `EventStore<TEvent>` implementation included for unit testing.

### IsTerminal
A predicate that identifies when an aggregate or saga has reached a final state. Once terminal, the entity is logically "done." Default implementation returns `false`.

## K

### KurrentDB
An event-native database (formerly EventStoreDB). Glauca provides `KurrentDBEventStore` in the `Picea.Glauca.KurrentDB` package.

## M

### Mealy Machine
A finite state machine where outputs depend on both the current state and the input event: $(S \times E) \rightarrow (S \times F)$. The fundamental model behind both Picea kernel and Glauca sagas.

## O

### Optimistic Concurrency
A concurrency control strategy that allows multiple readers but detects write conflicts at append time via version checking. No locks are held during reads.

## P

### Projection
A read model builder that folds over an event stream. Supports full replay (`Project`) and incremental catch-up (`CatchUp`).

## R

### Rebuild
Reloading all events from the store and reconstructing state from scratch. Useful when in-memory state may be stale.

### ResolvingAggregateRunner
An `AggregateRunner` variant that automatically resolves concurrency conflicts via the domain's `ConflictResolver.ResolveConflicts` method.

## S

### Saga
A long-running process that coordinates actions across aggregates by reacting to events and producing commands. Extends the Picea `Automaton` interface with `IsTerminal`.

### SagaRunner
The runtime for sagas. Handles event persistence, terminal state detection, and effect production.

### SequenceNumber
The 1-based position of an event in a stream. The first event has sequence number 1.

### StoredEvent
An event wrapped with persistence metadata: `SequenceNumber`, `Event`, and `Timestamp`.

### StreamId
A string identifier for an event stream. Typically encodes the aggregate type and instance ID (e.g., `"counter-1"`, `"order-42"`).

## T

### Terminal State
A state from which no further meaningful transitions occur. Aggregates with `IsTerminal = true` are logically complete. Sagas at terminal states ignore further events.

### Transition
A pure function that applies an event to the current state, producing new state and an effect: `(State, Event) → (State, Effect)`. The core of the Mealy machine model.

## V

### Version
The stream version — the sequence number of the last event. Used for optimistic concurrency control. A new stream has version `0`.
