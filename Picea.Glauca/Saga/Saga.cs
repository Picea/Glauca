// =============================================================================
// Saga — Process Manager interface as Mealy machine
// =============================================================================

using Picea;

namespace Picea.Glauca.Saga;

/// <summary>
/// A long-running process modeled as a Mealy machine (event-driven state machine
/// that produces effects/commands on transitions).
/// </summary>
/// <remarks>
/// <para>
/// Sagas react to domain events and produce commands (effects) for other
/// aggregates. Unlike a <see cref="Decider{TState, TCommand, TEvent, TEffect, TError, TParameters}"/>,
/// a saga does not <em>decide</em> — it <em>reacts</em>.
/// </para>
/// <para>
/// Sagas can reach a terminal state via <see cref="IsTerminal"/>.
/// Once terminal, events are ignored and no further effects are produced.
/// </para>
/// </remarks>
/// <typeparam name="TState">The saga state type (typically an enum).</typeparam>
/// <typeparam name="TEvent">The event union type the saga reacts to.</typeparam>
/// <typeparam name="TEffect">The command/effect type the saga produces.</typeparam>
/// <typeparam name="TParameters">Initialization parameters (use <see cref="Unit"/> if none).</typeparam>
public interface Saga<TState, TEvent, TEffect, TParameters>
    : Automaton<TState, TEvent, TEffect, TParameters>
{
    /// <summary>
    /// Determines whether the given state is terminal (the saga is complete).
    /// </summary>
    /// <remarks>
    /// Override this to define which states represent completed or cancelled sagas.
    /// The default implementation returns <c>false</c> (no terminal state).
    /// </remarks>
    static virtual bool IsTerminal(TState state) => false;
}
