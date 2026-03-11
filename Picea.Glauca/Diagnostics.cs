// =============================================================================
// Event Sourcing Diagnostics
// =============================================================================

using System.Diagnostics;

namespace Picea.Glauca;

/// <summary>
/// OpenTelemetry diagnostics for the event sourcing subsystem.
/// </summary>
internal static class EventSourcingDiagnostics
{
    public const string SourceName = "Picea.Glauca";
    public static readonly ActivitySource Source = new(SourceName);
}
