# Diagnostics

OpenTelemetry tracing support for Picea.Glauca operations.

## Activity Sources

Picea.Glauca publishes traces via two `System.Diagnostics.ActivitySource` instances:

| Source Name | Class | Covers |
| ----------- | ----- | ------ |
| `"Picea.Glauca"` | `EventSourcingDiagnostics` | `AggregateRunner`, `ResolvingAggregateRunner`, `KurrentDBEventStore` |
| `"Picea.Glauca.Saga"` | `SagaDiagnostics` | `SagaRunner` |

## Activities Created

### AggregateRunner / ResolvingAggregateRunner

| Activity | When |
| -------- | ---- |
| `"Handle"` | Processing a command |
| `"Load"` | Loading aggregate from event store |
| `"Rebuild"` | Rebuilding state from events |

### SagaRunner

| Activity | When |
| -------- | ---- |
| `"Handle"` | Processing an event through the saga |
| `"Load"` | Loading saga from event store |
| `"Rebuild"` | Rebuilding saga state |

### KurrentDBEventStore

| Activity | When |
| -------- | ---- |
| `"KurrentDB.Append"` | Appending events to KurrentDB |
| `"KurrentDB.Load"` | Loading all events from KurrentDB |
| `"KurrentDB.LoadAfter"` | Loading events after a version from KurrentDB |

## Subscribing to Traces

### OpenTelemetry SDK

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Picea.Glauca")
    .AddSource("Picea.Glauca.Saga")
    .AddConsoleExporter()       // or OTLP, Jaeger, Zipkin, etc.
    .Build();
```

### ASP.NET Core

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Picea.Glauca")
        .AddSource("Picea.Glauca.Saga")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

### ActivityListener (low-level)

```csharp
var listener = new ActivityListener
{
    ShouldListenTo = source =>
        source.Name is "Picea.Glauca" or "Picea.Glauca.Saga",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllData,
    ActivityStarted = activity =>
        Console.WriteLine($"Started: {activity.DisplayName}"),
    ActivityStopped = activity =>
        Console.WriteLine($"Stopped: {activity.DisplayName} ({activity.Duration})")
};

ActivitySource.AddActivityListener(listener);
```

## Source Code

```csharp
// EventSourcingDiagnostics.cs
internal static class EventSourcingDiagnostics
{
    internal static readonly ActivitySource Source = new("Picea.Glauca");
}

// SagaDiagnostics (in Saga/SagaRunner.cs)
internal static class SagaDiagnostics
{
    internal static readonly ActivitySource Source = new("Picea.Glauca.Saga");
}
```

## See Also

- [Microsoft: System.Diagnostics.ActivitySource](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
