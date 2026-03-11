// =============================================================================
// InMemoryEventStore — Thread-safe in-memory EventStore for testing
// =============================================================================

namespace Picea.Glauca;

/// <summary>
/// In-memory <see cref="EventStore{TEvent}"/> implementation for unit testing.
/// Thread-safe via <see cref="Lock"/>.
/// </summary>
/// <typeparam name="TEvent">The event union type.</typeparam>
public sealed class InMemoryEventStore<TEvent> : EventStore<TEvent>
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<StoredEvent<TEvent>>> _streams = new();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
        string streamId, TEvent[] events, long expectedVersion,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (events.Length == 0)
            return new(Array.Empty<StoredEvent<TEvent>>());

        lock (_lock)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
            {
                stream = [];
                _streams[streamId] = stream;
            }

            var actualVersion = (long)stream.Count;
            if (actualVersion != expectedVersion)
                throw new ConcurrencyException(streamId, expectedVersion, actualVersion);

            var stored = new List<StoredEvent<TEvent>>(events.Length);
            var now = DateTimeOffset.UtcNow;

            foreach (var @event in events)
            {
                var seq = stream.Count + 1;
                var storedEvent = new StoredEvent<TEvent>(seq, @event, now);
                stream.Add(storedEvent);
                stored.Add(storedEvent);
            }

            return new((IReadOnlyList<StoredEvent<TEvent>>)stored);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
                return new(Array.Empty<StoredEvent<TEvent>>());

            return new((IReadOnlyList<StoredEvent<TEvent>>)stream.ToList());
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, long afterVersion,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
                return new(Array.Empty<StoredEvent<TEvent>>());

            var result = stream
                .Where(e => e.SequenceNumber > afterVersion)
                .ToList();

            return new((IReadOnlyList<StoredEvent<TEvent>>)result);
        }
    }

    /// <summary>
    /// Gets the raw stream for test assertions. Not part of the <see cref="EventStore{TEvent}"/> contract.
    /// </summary>
    public IReadOnlyList<StoredEvent<TEvent>> GetStream(string streamId)
    {
        lock (_lock)
        {
            return _streams.TryGetValue(streamId, out var stream)
                ? stream.ToList()
                : Array.Empty<StoredEvent<TEvent>>();
        }
    }
}
