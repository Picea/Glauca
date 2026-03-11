# Picea.Glauca

Event Sourcing patterns modeled as Mealy machine automata — Aggregate runners, Saga orchestration, Projections, and pluggable EventStore adapters. Built on the [Picea](https://github.com/Picea/Picea) kernel.

## Packages

| Package | Description |
|---------|-------------|
| `Picea.Glauca` | Core event sourcing: `AggregateRunner`, `ResolvingAggregateRunner`, `SagaRunner`, `Projection`, `EventStore`, `InMemoryEventStore` |
| `Picea.Glauca.KurrentDB` | [KurrentDB](https://www.kurrent.io/) adapter for `EventStore<TEvent>` |

## License

Apache-2.0
