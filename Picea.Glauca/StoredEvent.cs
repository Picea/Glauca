// =============================================================================
// StoredEvent — Event envelope with metadata
// =============================================================================

namespace Picea.Glauca;

/// <summary>
/// An event wrapped with persistence metadata.
/// </summary>
/// <typeparam name="TEvent">The event union type.</typeparam>
/// <param name="SequenceNumber">The 1-based position in the stream.</param>
/// <param name="Event">The domain event.</param>
/// <param name="Timestamp">When the event was persisted.</param>
public readonly record struct StoredEvent<TEvent>(
    long SequenceNumber,
    TEvent Event,
    DateTimeOffset Timestamp);
