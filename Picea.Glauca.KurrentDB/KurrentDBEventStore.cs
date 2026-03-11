// =============================================================================
// KurrentDBEventStore — KurrentDB-backed EventStore implementation
// =============================================================================

using System.Runtime.CompilerServices;

using KurrentDB.Client;

using Picea.Glauca;

namespace Picea.Glauca.KurrentDB;

/// <summary>
/// <see cref="EventStore{TEvent}"/> implementation backed by KurrentDB.
/// </summary>
/// <remarks>
/// <para>
/// Uses delegate-based serialization to decouple the event store from
/// any specific serialization library. The caller provides
/// <c>serialize</c> and <c>deserialize</c> functions.
/// </para>
/// <para>
/// KurrentDB uses 0-based stream revisions internally. This adapter
/// maps between Picea’s 1-based <see cref="StoredEvent{TEvent}.SequenceNumber"/>
/// and KurrentDB’s 0-based <see cref="StreamRevision"/>.
/// </para>
/// </remarks>
/// <typeparam name="TEvent">The event union type.</typeparam>
public sealed class KurrentDBEventStore<TEvent> : EventStore<TEvent>
{
    private readonly KurrentDBClient _client;
    private readonly Func<TEvent, (string EventType, ReadOnlyMemory<byte> Data)> _serialize;
    private readonly Func<string, ReadOnlyMemory<byte>, TEvent> _deserialize;

    /// <summary>
    /// Creates a new KurrentDB event store.
    /// </summary>
    /// <param name="client">The KurrentDB client.</param>
    /// <param name="serialize">Serializes an event to (eventType, data).</param>
    /// <param name="deserialize">Deserializes (eventType, data) back to an event.</param>
    public KurrentDBEventStore(
        KurrentDBClient client,
        Func<TEvent, (string EventType, ReadOnlyMemory<byte> Data)> serialize,
        Func<string, ReadOnlyMemory<byte>, TEvent> deserialize)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
        _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
    }

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> AppendAsync(
        string streamId, TEvent[] events, long expectedVersion,
        CancellationToken ct = default)
    {
        if (events.Length == 0)
            return Array.Empty<StoredEvent<TEvent>>();

        var eventData = events.Select(e =>
        {
            var (eventType, data) = _serialize(e);
            return new EventData(Uuid.NewUuid(), eventType, data);
        }).ToArray();

        try
        {
            // Map 1-based expectedVersion to KurrentDB's StreamState/StreamRevision
            var writeResult = expectedVersion == 0
                ? await _client.AppendToStreamAsync(
                    streamId, StreamState.NoStream, eventData, cancellationToken: ct)
                : await _client.AppendToStreamAsync(
                    streamId, new StreamRevision((ulong)(expectedVersion - 1)),
                    eventData, cancellationToken: ct);

            // Build stored events with correct sequence numbers
            var stored = new List<StoredEvent<TEvent>>(events.Length);
            var now = DateTimeOffset.UtcNow;

            for (var i = 0; i < events.Length; i++)
            {
                stored.Add(new StoredEvent<TEvent>(
                    expectedVersion + i + 1,
                    events[i],
                    now));
            }

            return stored;
        }
        catch (WrongExpectedVersionException)
        {
            // Load current stream to get actual version
            var current = await LoadAsync(streamId, ct);
            var actualVersion = current.Count > 0
                ? current[^1].SequenceNumber
                : 0;
            throw new ConcurrencyException(streamId, expectedVersion, actualVersion);
        }
    }

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, CancellationToken ct = default)
    {
        try
        {
            var result = _client.ReadStreamAsync(streamId, Direction.Forwards,
                StreamPosition.Start, cancellationToken: ct);

            var events = new List<StoredEvent<TEvent>>();
            long seq = 1;

            await foreach (var resolved in result)
            {
                var @event = _deserialize(
                    resolved.Event.EventType,
                    resolved.Event.Data);

                events.Add(new StoredEvent<TEvent>(
                    seq++, @event, resolved.Event.Created));
            }

            return events;
        }
        catch (StreamNotFoundException)
        {
            return Array.Empty<StoredEvent<TEvent>>();
        }
    }

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<IReadOnlyList<StoredEvent<TEvent>>> LoadAsync(
        string streamId, long afterVersion,
        CancellationToken ct = default)
    {
        try
        {
            // afterVersion is 1-based, KurrentDB StreamPosition is 0-based
            var startPosition = new StreamPosition((ulong)afterVersion);

            var result = _client.ReadStreamAsync(streamId, Direction.Forwards,
                startPosition, cancellationToken: ct);

            var events = new List<StoredEvent<TEvent>>();
            long seq = afterVersion + 1;

            await foreach (var resolved in result)
            {
                var @event = _deserialize(
                    resolved.Event.EventType,
                    resolved.Event.Data);

                events.Add(new StoredEvent<TEvent>(
                    seq++, @event, resolved.Event.Created));
            }

            return events;
        }
        catch (StreamNotFoundException)
        {
            return Array.Empty<StoredEvent<TEvent>>();
        }
    }
}
