# Tutorials

End-to-end walkthroughs that build complete working systems with Picea.Glauca. Each tutorial starts from scratch and ends with running, tested code.

> **New to Picea.Glauca?** Start with the [Quick Start](../getting-started/index.md) for a 5-minute introduction, then come back here for deeper dives.

## Prerequisites

- .NET 10.0 SDK ([installation guide](../getting-started/installation.md))
- `dotnet add package Picea.Glauca`

## Tutorials

| # | Tutorial | What You'll Build | Concepts Used |
|---|----------|-------------------|---------------|
| 01 | [First Aggregate](01-first-aggregate.md) | A bank account with deposits, withdrawals, and overdraft protection | [Event Sourcing](../concepts/event-sourcing.md), [AggregateRunner](../concepts/aggregate-runner.md) |
| 02 | [Projections](02-projections.md) | Multiple read models from a single event stream | [Projections](../concepts/projections.md) |
| 03 | [Conflict Resolution](03-conflict-resolution.md) | A shared counter with automatic concurrent merge | [Conflict Resolution](../concepts/conflict-resolution.md) |
| 04 | [Sagas](04-sagas.md) | An order fulfillment workflow across aggregates | [Sagas](../concepts/sagas.md) |

## The Big Idea

Every tutorial builds on the **same decider pattern** from the [Picea kernel](https://github.com/Picea/Picea):

```text
decide    : (State × Command) → Result<Event[], Error>
transition : (State × Event)   → (State × Effect)
```

You write your domain logic once as pure functions. The runner handles persistence, concurrency, and lifecycle. Each tutorial shows a different facet of event sourcing — aggregates, read models, conflict resolution, or long-running processes — without changing the domain logic.

## Recommended Reading Order

```text
First Aggregate (01)
        │
        ├──► Projections (02)
        │
        ├──► Conflict Resolution (03)
        │
        └──► Sagas (04)
```

1. **[First Aggregate](01-first-aggregate.md)** — the core loop: command → event → state.
2. **[Projections](02-projections.md)** — building read models from the event stream.
3. **[Conflict Resolution](03-conflict-resolution.md)** — handling concurrent writes.
4. **[Sagas](04-sagas.md)** — coordinating across aggregates.

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Understand the theory | [Concepts](../concepts/index.md) |
| Solve a specific problem | [How-To Guides](../guides/index.md) |
| Look up an API | [Reference](../reference/index.md) |
| See design rationale | [Architecture Decision Records](../adr/) |
