// =============================================================================
// ConflictResolver — Domain-aware optimistic concurrency resolution
// =============================================================================

using Picea;

namespace Picea.Glauca;

/// <summary>
/// A <see cref="Decider{TState, TCommand, TEvent, TEffect, TError, TParameters}"/> that
/// can resolve concurrency conflicts by examining the concurrent event streams.
/// </summary>
/// <remarks>
/// <para>
/// When a <see cref="ConcurrencyException"/> occurs during event appending,
/// the <see cref="ResolvingAggregateRunner{TDecider, TState, TCommand, TEvent, TEffect, TError, TParameters}"/>
/// calls <see cref="ResolveConflicts"/> to attempt automatic resolution.
/// </para>
/// <para>
/// The resolution contract:
/// <list type="bullet">
///   <item><c>currentState</c> — state after applying <em>their</em> events to the last known state</item>
///   <item><c>projectedState</c> — state after applying <em>our</em> events on top of <c>currentState</c></item>
///   <item><c>ourEvents</c> — the events we tried to append</item>
///   <item><c>theirEvents</c> — the events that were appended by another process since our last read</item>
/// </list>
/// Return <c>Ok(events)</c> to retry appending, or <c>Err(ConflictNotResolved)</c> to fail.
/// </para>
/// </remarks>
public interface ConflictResolver<TState, TCommand, TEvent, TEffect, TError, TParameters>
    : Decider<TState, TCommand, TEvent, TEffect, TError, TParameters>
{
    /// <summary>
    /// Attempts to resolve a concurrency conflict between our events and
    /// events that were concurrently appended by another process.
    /// </summary>
    /// <param name="currentState">
    /// The state after applying <paramref name="theirEvents"/> to the
    /// last known state.
    /// </param>
    /// <param name="projectedState">
    /// The state after applying <paramref name="ourEvents"/> on top of
    /// <paramref name="currentState"/>. Pre-computed by the runner via
    /// <c>Transition</c> for convenience.
    /// </param>
    /// <param name="ourEvents">The events we tried to append.</param>
    /// <param name="theirEvents">
    /// The events that were concurrently appended since our last read.
    /// </param>
    /// <returns>
    /// <see cref="Result{TOk, TErr}.Ok"/> with events to retry appending
    /// (may be the same as <paramref name="ourEvents"/> or a modified set),
    /// or <see cref="Result{TOk, TErr}.Err"/> with <see cref="ConflictNotResolved"/>
    /// if the conflict cannot be resolved.
    /// </returns>
    static abstract Result<TEvent[], ConflictNotResolved> ResolveConflicts(
        TState currentState,
        TState projectedState,
        TEvent[] ourEvents,
        IReadOnlyList<TEvent> theirEvents);
}

/// <summary>
/// Indicates that a concurrency conflict could not be resolved by the
/// domain's <see cref="ConflictResolver{TState, TCommand, TEvent, TEffect, TError, TParameters}"/>.
/// </summary>
/// <param name="Reason">A human-readable explanation of why the conflict was not resolved.</param>
public readonly record struct ConflictNotResolved(string Reason);
