// =============================================================================
// Projection — Read model builder via fold over event streams
// =============================================================================

using System.Runtime.CompilerServices;

namespace Picea.Glauca;

/// <summary>
/// Builds a read model by folding over an event stream.
/// Supports both full replay via <see cref="Project"/> and incremental
/// catch-up via <see cref="CatchUp"/>.
/// </summary>
/// <typeparam name="TEvent">The event union type.</typeparam>
/// <typeparam name="TReadModel">The read model type.</typeparam>
public sealed class Projection<TEvent, TReadModel>
{
    private readonly Func<TReadModel, TEvent, TReadModel> _apply;
    private TReadModel _readModel;
    private long _lastProcessedVersion;

    /// <summary>
    /// Creates a new projection with the specified initial read model and apply function.
    /// </summary>
    /// <param name="initial">The initial read model state.</param>
    /// <param name="apply">A fold function: (currentReadModel, event) → newReadModel.</param>
    public Projection(TReadModel initial, Func<TReadModel, TEvent, TReadModel> apply)
    {
        _readModel = initial;
        _apply = apply;
    }

    /// <summary>The current read model state.</summary>
    public TReadModel ReadModel => _readModel;

    /// <summary>The version (sequence number) of the last processed event.</summary>
    public long LastProcessedVersion => _lastProcessedVersion;

    /// <summary>
    /// Projects the entire stream from the beginning, replacing the current read model.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<TReadModel> Project(
        EventStore<TEvent> store, string streamId,
        CancellationToken ct = default)
    {
        var events = await store.LoadAsync(streamId, ct);
        foreach (var stored in events)
        {
            _readModel = _apply(_readModel, stored.Event);
            _lastProcessedVersion = stored.SequenceNumber;
        }
        return _readModel;
    }

    /// <summary>
    /// Catches up by processing only events after the last processed version.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<TReadModel> CatchUp(
        EventStore<TEvent> store, string streamId,
        CancellationToken ct = default)
    {
        var events = await store.LoadAsync(streamId, _lastProcessedVersion, ct);
        foreach (var stored in events)
        {
            _readModel = _apply(_readModel, stored.Event);
            _lastProcessedVersion = stored.SequenceNumber;
        }
        return _readModel;
    }

    /// <summary>
    /// Applies a single raw event (without persistence metadata).
    /// Does not update <see cref="LastProcessedVersion"/>.
    /// </summary>
    public void Apply(TEvent @event)
    {
        _readModel = _apply(_readModel, @event);
    }

    /// <summary>
    /// Applies a stored event and updates <see cref="LastProcessedVersion"/>.
    /// </summary>
    public void Apply(StoredEvent<TEvent> stored)
    {
        _readModel = _apply(_readModel, stored.Event);
        _lastProcessedVersion = stored.SequenceNumber;
    }
}
