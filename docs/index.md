# Picea.Glauca Documentation

Welcome to the Picea.Glauca documentation. Picea.Glauca provides event sourcing patterns modeled as Mealy machine automata — built on the [Picea](https://github.com/Picea/Picea) kernel.

## Quick Navigation

| Section | What You'll Find |
| ------- | ---------------- |
| [Getting Started](getting-started/index.md) | Installation, first aggregate, 5-minute quick start |
| [Concepts](concepts/index.md) | Event sourcing, aggregates, projections, sagas, conflict resolution |
| [Tutorials](tutorials/README.md) | End-to-end walkthroughs building real systems |
| [How-To Guides](guides/index.md) | Recipes for specific tasks |
| [Reference](reference/index.md) | Complete API documentation |
| [ADRs](adr/) | Architecture decision records |

## The Big Idea

Write your domain logic once as a pure [Picea `Decider`](https://github.com/Picea/Picea):

```csharp
public static Result<TEvent[], TError> Decide(TState state, TCommand command) => ...
public static (TState, TEffect) Transition(TState state, TEvent @event) => ...
```

Then Glauca handles the rest:

| Component | What It Does |
| --------- | ------------ |
| **AggregateRunner** | Loads events → rebuilds state → handles commands → appends events |
| **ResolvingAggregateRunner** | Same, but with automatic conflict resolution on concurrency conflicts |
| **SagaRunner** | Reacts to events → produces commands for other aggregates |
| **Projection** | Folds events into read models (full replay + incremental catch-up) |
| **EventStore** | Pluggable persistence — `InMemoryEventStore` for testing, `KurrentDBEventStore` for production |

Same `Decide` + `Transition` functions. The runner handles persistence, concurrency, and lifecycle.

## Architecture

```text
Picea Kernel (Automaton + Decider)        ← pure domain logic
    │
    └── Picea.Glauca                      ← event sourcing runtime
            │
            ├── EventStore<TEvent>        ← persistence abstraction
            │   ├── InMemoryEventStore    ← testing
            │   └── KurrentDBEventStore   ← production (separate package)
            │
            ├── AggregateRunner           ← command → event → state loop
            │   └── ResolvingAggregateRunner  ← with conflict resolution
            │
            ├── SagaRunner                ← event → effect coordination
            │
            ├── Projection                ← event → read model fold
            │
            └── Diagnostics               ← OpenTelemetry tracing
```

## Documentation Conventions

- **Concepts** explain *why* — the theory and design rationale
- **Tutorials** show *how* — step-by-step walkthroughs
- **Guides** solve *specific problems* — recipes you can apply immediately
- **Reference** documents *what* — complete API signatures and behavior

This follows the [Diataxis](https://diataxis.fr/) documentation framework.
