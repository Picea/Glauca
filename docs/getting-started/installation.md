# Installation

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Picea](https://www.nuget.org/packages/Picea) (pulled in automatically as a transitive dependency)

## Install via NuGet

### Core Package

```bash
dotnet add package Picea.Glauca
```

Or add to your `.csproj`:

```xml
<PackageReference Include="Picea.Glauca" Version="0.1.*" />
```

### KurrentDB Adapter (optional)

For production persistence with [KurrentDB](https://www.kurrent.io/):

```bash
dotnet add package Picea.Glauca.KurrentDB
```

## Verify Installation

```csharp
using Picea.Glauca;

// Create an in-memory event store — if this compiles, you're good
var store = new InMemoryEventStore<string>();
Console.WriteLine("Picea.Glauca is installed!");
```

## Package Ecosystem

| Package | Description |
| ------- | ----------- |
| `Picea` | The kernel — Automaton, Runtime, Decider, Result |
| **`Picea.Glauca`** | Event Sourcing — AggregateRunner, SagaRunner, Projection, EventStore |
| **`Picea.Glauca.KurrentDB`** | KurrentDB adapter for `EventStore<TEvent>` |
| `Picea.Abies` | MVU (Model-View-Update) runtime for Blazor |

## Dependency Graph

```text
Picea.Glauca.KurrentDB
    └── Picea.Glauca
        └── Picea (kernel)
            └── .NET BCL only (zero external deps)
```

## See Also

- [Quick Start](index.md) — 5-minute getting started guide
- [KurrentDB Setup](../guides/kurrentdb-setup.md) — production database configuration
