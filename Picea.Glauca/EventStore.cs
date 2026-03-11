// =============================================================================
// EventStore — Async event persistence abstraction
// =============================================================================

namespace Picea.Glauca;

/// <summary>
/// Asynchronous event store abstraction for append-only event streams
/// with optimistic concurrency control.
/// </summary>
/// <typeparam name="TEvent">The event union type.</typeparam>
public interface EventStore<TEvent>
{
    /// <summary>
    /// Appends events to a stream with optimistic concurrency.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="events">Events to append.</param>
    /// <param name="expectedVersion">
    /// The expected current version (sequence number) of the stream.
    /// Use <c>0</c> for a new stream.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored events with assigned sequence numbers and timestamps.</returns>
    /// <exception cref="ConcurrencyException">
    /// Thrown when <paramref name="expectedVersion"/> does not match the actual stream version.
    /// </exception>
    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
        string streamId, TEvent[] events, long expectedVersion,
        CancellationToken ct = default);

    /// <summary>
    /// Loads all events from a stream.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All stored events in order, or an empty list if the stream does not exist.</returns>
    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, CancellationToken ct = default);

    /// <summary>
    /// Loads events from a stream starting after a given version.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="afterVersion">
    /// The version after which to start reading. Only events with
    /// <see cref="StoredEvent{TEvent}.SequenceNumber"/> greater than
    /// this value are returned.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stored events after the specified version, or empty if none exist.</returns>
    ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, long afterVersion,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when an optimistic concurrency check fails during event appending.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    /// <summary>The stream where the conflict occurred.</summary>
    public string StreamId { get; }

    /// <summary>The version the caller expected.</summary>
    public long ExpectedVersion { get; }

    /// <summary>The actual version of the stream.</summary>
    public long ActualVersion { get; }

    /// <summary>
    /// Creates a new <see cref="ConcurrencyException"/>.
    /// </summary>
    public ConcurrencyException(string streamId, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict on stream '{streamId}': expected version {expectedVersion}, actual {actualVersion}.")
    {
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
