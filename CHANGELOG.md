# Changelog

All notable changes to Picea.Glauca will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
via [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning).

## [Unreleased]

### Fixed

- **AggregateRunner / ResolvingAggregateRunner**: `IsTerminal` now delegates to `TDecider.IsTerminal(_state)` instead of using a dead `_isTerminal` field that was never assigned. Terminal state detection is now correct.

### Added

- **Documentation**: Full Diataxis-structured docs — concepts, getting started, tutorials, how-to guides, API reference
- **CHANGELOG.md**: This file
- **CONTRIBUTING.md**: Contribution guidelines (trunk-based development)
- **SECURITY.md**: Security policy and vulnerability reporting

### Changed

- **README.md**: Comprehensive rewrite with installation, quick start, projections, conflict resolution, sagas, KurrentDB adapter, OpenTelemetry, API reference, and ecosystem table

## [0.1.13] — 2026-03-13

### Fixed

- **CI/CD**: Fixed GitHub Actions CD pipeline — `setup-dotnet@v5` with `dotnet-quality: preview`, PATH fix for `nbgv` tool, `global.json` SDK version constraint

### Added

- **NuGet**: First successful publication of both packages to nuget.org

## [0.1.0] — 2026-03-09

### Added

- **AggregateRunner**: Event-sourced aggregate runtime with persistence, optimistic concurrency, and lifecycle management
- **ResolvingAggregateRunner**: Aggregate runner with automatic conflict resolution (up to 3 retries)
- **ConflictResolver**: Domain-aware optimistic concurrency resolution interface
- **SagaRunner**: Event-sourced saga runtime with terminal state support
- **Saga**: Process manager interface modeled as a Mealy machine
- **Projection**: Read model builder via fold over event streams (full replay + incremental catch-up)
- **EventStore**: Async event persistence abstraction with optimistic concurrency control
- **InMemoryEventStore**: Thread-safe in-memory implementation for unit testing
- **StoredEvent**: Event envelope with sequence number, event, and timestamp
- **ConcurrencyException**: Thrown on optimistic concurrency version mismatch
- **KurrentDBEventStore**: Production adapter for KurrentDB with delegate-based serialization
- **Diagnostics**: OpenTelemetry-compatible `ActivitySource` tracing (`Picea.Glauca`, `Picea.Glauca.Saga`)

[Unreleased]: https://github.com/picea/glauca/compare/v0.1.13...HEAD
[0.1.13]: https://github.com/picea/glauca/releases/tag/v0.1.13
[0.1.0]: https://github.com/picea/glauca/releases/tag/v0.1.0
