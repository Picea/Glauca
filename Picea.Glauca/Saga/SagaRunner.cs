// =============================================================================
// SagaRunner — Event-sourced saga runtime
// =============================================================================

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Picea;

namespace Picea.Glauca.Saga;

/// <summary>
/// OpenTelemetry diagnostics for the saga subsystem.
/// </summary>
internal static class SagaDiagnostics
{
    public const string SourceName = "Picea.Glauca.Saga";
    public static readonly ActivitySource Source = new(SourceName);
}

/// <summary>
/// Event-sourced saga runner that persists events and coordinates
/// the saga's transitions and effect production.
/// </summary>
/// <typeparam name="TSaga">The saga type implementing <see cref="Saga{TState, TEvent, TEffect, TParameters}"/>.</typeparam>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <typeparam name="TEvent">The event union type.</typeparam>
/// <typeparam name="TEffect">The command/effect type the saga produces.</typeparam>
/// <typeparam name="TParameters">Initialization parameters (use <see cref="Unit"/> if none).</typeparam>
public sealed class SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>
    : IDisposable
    where TSaga : Saga<TState, TEvent, TEffect, TParameters>
{
    private readonly EventStore<TEvent> _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TState _state;
    private long _version;
    private readonly List<TEffect> _effects = [];
    private bool _isTerminal;

    private SagaRunner(EventStore<TEvent> store, string streamId,
        TState state, long version, TParameters parameters)
    {
        _store = store;
        StreamId = streamId;
        _state = state;
        _version = version;
        Parameters = parameters;
        _isTerminal = TSaga.IsTerminal(state);
    }

    /// <summary>The stream identifier this saga is bound to.</summary>
    public string StreamId { get; }

    /// <summary>Initialization parameters.</summary>
    public TParameters Parameters { get; }

    /// <summary>Current saga state.</summary>
    public TState State => _state;

    /// <summary>Current version (number of events in the stream).</summary>
    public long Version => _version;

    /// <summary>Accumulated effects from all handled events.</summary>
    public IReadOnlyList<TEffect> Effects => _effects;

    /// <summary>Whether the saga has reached a terminal state.</summary>
    public bool IsTerminal => _isTerminal;

    /// <summary>
    /// Creates a new saga runner with initial state.
    /// </summary>
    public static SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>
        Create(EventStore<TEvent> store, string streamId, TParameters parameters)
    {
        var (state, effect) = TSaga.Initialize(parameters);
        return new SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>(
            store, streamId, state, 0, parameters);
    }

    /// <summary>
    /// Loads a saga by replaying all events from the stream.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>>
        Load(EventStore<TEvent> store, string streamId, TParameters parameters,
            CancellationToken ct = default)
    {
        var (state, _) = TSaga.Initialize(parameters);
        var runner = new SagaRunner<TSaga, TState, TEvent, TEffect, TParameters>(
            store, streamId, state, 0, parameters);

        var events = await store.LoadAsync(streamId, ct);
        foreach (var stored in events)
        {
            var (newState, effect) = TSaga.Transition(runner._state, stored.Event);
            runner._state = newState;
            runner._version = stored.SequenceNumber;
            runner._isTerminal = TSaga.IsTerminal(newState);
        }

        return runner;
    }

    /// <summary>
    /// Handles an incoming event: transitions state, persists, and returns the effect.
    /// </summary>
    /// <returns>The effect/command produced by the transition.</returns>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<TEffect> Handle(TEvent @event, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var activity = SagaDiagnostics.Source
                .StartActivity($"Handle {typeof(TEvent).Name}");

            // Terminal sagas ignore further events
            if (_isTerminal)
            {
                var (_, initEffect) = TSaga.Initialize(Parameters);
                return initEffect;
            }

            var (newState, effect) = TSaga.Transition(_state, @event);

            // Only persist if state actually changed
            if (!EqualityComparer<TState>.Default.Equals(_state, newState))
            {
                var stored = await _store.AppendAsync(StreamId, [@event], _version, ct);
                _version = stored[^1].SequenceNumber;
                _state = newState;
                _isTerminal = TSaga.IsTerminal(newState);
                _effects.Add(effect);
            }

            return effect;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
