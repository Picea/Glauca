// =============================================================================
// ResolvingAggregateRunner — Aggregate runner with automatic conflict resolution
// =============================================================================

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Picea;

namespace Picea.Glauca;

/// <summary>
/// Event-sourced aggregate runner with automatic conflict resolution.
/// When a <see cref="ConcurrencyException"/> occurs, the runner loads
/// the conflicting events and delegates to the decider's
/// <see cref="ConflictResolver{TState, TCommand, TEvent, TEffect, TError, TParameters}.ResolveConflicts"/>
/// method to attempt automatic resolution.
/// </summary>
/// <typeparam name="TDecider">The decider type implementing <see cref="ConflictResolver{TState, TCommand, TEvent, TEffect, TError, TParameters}"/>.</typeparam>
/// <typeparam name="TState">The aggregate state type.</typeparam>
/// <typeparam name="TCommand">The command union type.</typeparam>
/// <typeparam name="TEvent">The event union type.</typeparam>
/// <typeparam name="TEffect">The effect/side-effect union type.</typeparam>
/// <typeparam name="TError">The error union type.</typeparam>
/// <typeparam name="TParameters">Initialization parameters (use <see cref="Unit"/> if none).</typeparam>
public sealed class ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
    : IDisposable
    where TDecider : ConflictResolver<TState, TCommand, TEvent, TEffect, TError, TParameters>
{
    private const int MaxRetries = 3;

    private readonly EventStore<TEvent> _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TState _state;
    private long _version;
    private readonly List<TEffect> _effects = [];
    private bool _isTerminal;

    private ResolvingAggregateRunner(EventStore<TEvent> store, string streamId,
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
    /// Creates a new resolving aggregate runner with initial state.
    /// </summary>
    public static ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>
        Create(EventStore<TEvent> store, string streamId, TParameters parameters)
    {
        var (state, effect) = TDecider.Initialize(parameters);
        return new ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>(
            store, streamId, state, 0, parameters);
    }

    /// <summary>
    /// Loads a resolving aggregate by replaying all events from the stream.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>>
        Load(EventStore<TEvent> store, string streamId, TParameters parameters,
            CancellationToken ct = default)
    {
        var (state, _) = TDecider.Initialize(parameters);
        var runner = new ResolvingAggregateRunner<TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters>(
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
    /// Handles a command with automatic conflict resolution on concurrency conflicts.
    /// </summary>
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

            return await AppendWithResolution(events, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Attempts to append events, resolving conflicts up to <see cref="MaxRetries"/> times.
    /// </summary>
    private async ValueTask<Result<TState, TError>> AppendWithResolution(
        TEvent[] ourEvents, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var stored = await _store.AppendAsync(StreamId, ourEvents, _version, ct);

                foreach (var s in stored)
                {
                    var (newState, effect) = TDecider.Transition(_state, s.Event);
                    _state = newState;
                    _version = s.SequenceNumber;
                    _effects.Add(effect);
                }

                return Result<TState, TError>.Ok(_state);
            }
            catch (ConcurrencyException) when (attempt < MaxRetries)
            {
                // Load events we missed
                var theirStoredEvents = await _store.LoadAsync(StreamId, _version, ct);

                // Apply their events to get current state
                var currentState = _state;
                foreach (var s in theirStoredEvents)
                {
                    var (newState, _) = TDecider.Transition(currentState, s.Event);
                    currentState = newState;
                }

                // Project our events on top to get projected state
                var projectedState = currentState;
                foreach (var e in ourEvents)
                {
                    var (newState, _) = TDecider.Transition(projectedState, e);
                    projectedState = newState;
                }

                // Ask the domain to resolve
                var theirEvents = theirStoredEvents.Select(s => s.Event).ToList();
                var resolution = TDecider.ResolveConflicts(
                    currentState, projectedState, ourEvents, theirEvents);

                if (resolution.IsErr)
                {
                    // Cannot resolve — convert to ConcurrencyException at boundary
                    var lastTheir = theirStoredEvents[^1];
                    throw new ConcurrencyException(StreamId, _version, lastTheir.SequenceNumber);
                }

                // Update our state to reflect their events
                foreach (var s in theirStoredEvents)
                {
                    var (newState, effect) = TDecider.Transition(_state, s.Event);
                    _state = newState;
                    _version = s.SequenceNumber;
                }

                // Retry with the resolved events
                ourEvents = resolution.Value;
            }
        }

        // Should not reach here, but safety net
        throw new ConcurrencyException(StreamId, _version, _version);
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
