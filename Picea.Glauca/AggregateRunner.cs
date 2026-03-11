// =============================================================================
// AggregateRunner — Event-sourced aggregate runtime
// =============================================================================

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Picea;

namespace Picea.Glauca;

/// <summary>
/// Event-sourced aggregate runner that wraps a <see cref="Decider{TState, TCommand, TEvent, TEffect, TError, TParameters}"/>
/// and provides persistence, concurrency control, and lifecycle management.
/// </summary>
/// <remarks>
/// <para>
/// The runner loads events from a <see cref="EventStore{TEvent}"/>, replays them
/// through the decider's <c>Transition</c> to rebuild state, and appends new events
/// on successful command handling. Optimistic concurrency is enforced: if the stream
/// has been modified since the last known version, <see cref="ConcurrencyException"/> is thrown.
/// </para>
/// <para>
/// Thread safety is provided via a <see cref="SemaphoreSlim"/> gate — concurrent
/// <see cref="Handle"/> calls are serialized without blocking the async pipeline.
/// </para>
/// </remarks>
/// <typeparam name="TDecider">The decider type implementing <see cref="Decider{TState, TCommand, TEvent, TEffect, TError, TParameters}"/>.</typeparam>
/// <typeparam name="TState">The aggregate state type.</typeparam>
/// <typeparam name="TCommand">The command union type.</typeparam>
/// <typeparam name="TEvent">The event union type.</typeparam>
/// <typeparam name="TEffect">The effect/side-effect union type.</typeparam>
/// <typeparam name="TError">The error union type.</typeparam>
/// <typeparam name="TParameters">Initialization parameters (use <see cref="Unit"/> if none).</typeparam>
public sealed class AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
    : IDisposable
    where TDecider : Decider<TState, TCommand, TEvent, TEffect, TError, TParameters>
{
    private readonly EventStore<TEvent> _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TState _state;
    private long _version;
    private readonly List<TEffect> _effects = [];
    private bool _isTerminal;

    private AggregateRunner(EventStore<TEvent> store, string streamId,
        TState state, long version, TParameters parameters)
    {
        _store = store;
        StreamId = streamId;
        _state = state;
        _version = version;
        Parameters = parameters;
    }

    /// <summary>The stream identifier this aggregate is bound to.</summary>
    public string StreamId { get; }

    /// <summary>Initialization parameters passed to the decider.</summary>
    public TParameters Parameters { get; }

    /// <summary>Current state after replaying all events.</summary>
    public TState State => _state;

    /// <summary>Current version (number of events in the stream).</summary>
    public long Version => _version;

    /// <summary>Accumulated effects from all handled commands.</summary>
    public IReadOnlyList<TEffect> Effects => _effects;

    /// <summary>Whether the aggregate has reached a terminal state.</summary>
    public bool IsTerminal => _isTerminal;

    /// <summary>
    /// Creates a new aggregate runner with initial state (no events loaded).
    /// </summary>
    public static AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
        Create(EventStore<TEvent> store, string streamId, TParameters parameters)
    {
        var (state, effect) = TDecider.Initialize(parameters);
        return new AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>(
            store, streamId, state, 0, parameters);
    }

    /// <summary>
    /// Loads an aggregate by replaying all events from the stream.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>>
        Load(EventStore<TEvent> store, string streamId, TParameters parameters,
            CancellationToken ct = default)
    {
        var (state, _) = TDecider.Initialize(parameters);
        var runner = new AggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>(
            store, streamId, state, 0, parameters);

        var events = await store.LoadAsync(streamId, ct);
        foreach (var stored in events)
        {
            var (newState, effect) = TDecider.Transition(runner._state, stored.Event);
            runner._state = newState;
            runner._version = stored.SequenceNumber;
        }

        return runner;
    }

    /// <summary>
    /// Handles a command: decides → appends events → transitions state.
    /// </summary>
    /// <returns>
    /// <see cref="Result{TOk, TErr}.Ok"/> with the new state on success,
    /// or <see cref="Result{TOk, TErr}.Err"/> with the domain error.
    /// </returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<Result<TState, TError>> Handle(
        TCommand command, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var activity = EventSourcingDiagnostics.Source
                .StartActivity($"Handle {typeof(TCommand).Name}");

            var decision = TDecider.Decide(_state, command);

            if (decision.IsErr)
                return Result<TState, TError>.Err(decision.Error);

            var events = decision.Value;

            if (events.Length == 0)
                return Result<TState, TError>.Ok(_state);

            // Append to store — throws ConcurrencyException on conflict
            var stored = await _store.AppendAsync(StreamId, events, _version, ct);

            // Transition state for each persisted event
            foreach (var s in stored)
            {
                var (newState, effect) = TDecider.Transition(_state, s.Event);
                _state = newState;
                _version = s.SequenceNumber;
                _effects.Add(effect);
            }

            return Result<TState, TError>.Ok(_state);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Rebuilds state from scratch by reloading all events from the store.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<TState> Rebuild(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var (state, _) = TDecider.Initialize(Parameters);
            _state = state;
            _version = 0;
            _effects.Clear();

            var events = await _store.LoadAsync(StreamId, ct);
            foreach (var stored in events)
            {
                var (newState, effect) = TDecider.Transition(_state, stored.Event);
                _state = newState;
                _version = stored.SequenceNumber;
            }

            return _state;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
