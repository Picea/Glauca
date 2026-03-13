# Concepts

Deep-dive explanations of the core ideas behind Picea.Glauca. Read these to understand *why* the library is designed the way it is.

> **New here?** Start with the [Quick Start](../getting-started/index.md) for a hands-on introduction, then come back for the theory.

## Core Concepts

| Concept | What It Explains |
| ------- | ---------------- |
| [Event Sourcing](event-sourcing.md) | Why persist events, not state — the foundational pattern |
| [The Aggregate Runner](aggregate-runner.md) | The command → event → state loop with persistence |
| [Projections](projections.md) | Building read models by folding over event streams |
| [Sagas](sagas.md) | Long-running processes as Mealy machines |
| [Conflict Resolution](conflict-resolution.md) | Domain-aware optimistic concurrency resolution |
| [Glossary](glossary.md) | Definitions of terms used throughout the docs |

## Reading Order

```text
Event Sourcing (start here)
    │
    ├─▶ The Aggregate Runner (how commands become persisted events)
    │       │
    │       ├─▶ Projections (building read models from events)
    │       │
    │       └─▶ Conflict Resolution (handling concurrent writes)
    │
    └─▶ Sagas (coordinating across aggregates)
```

## The One-Sentence Summary

Picea.Glauca turns a pure [Picea `Decider`](https://github.com/Picea/Picea) into a fully persistent, concurrency-safe event-sourced aggregate — the runner handles the I/O, the domain logic stays pure.
