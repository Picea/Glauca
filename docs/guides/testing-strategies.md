# Testing Strategies

How to test event-sourced code at every level: pure domain logic, runner behaviour, and end-to-end integration.

## The Testing Pyramid for Event Sourcing

```text
┌───────────────────────┐
│  Integration / E2E    │  Few — test the runner + real store
├───────────────────────┤
│  Runner behaviour     │  Some — test InMemoryEventStore
├───────────────────────┤
│  Pure domain logic    │  Many — test Decide/Transition directly
└───────────────────────┘
```

Most tests should live at the bottom: pure function calls against `Decide` and `Transition` with no infrastructure at all.

## Level 1: Pure Domain Logic

The decider pattern makes domain logic trivially testable. No mocking, no async, no setup:

```csharp
[Fact]
public void Deposit_increases_balance()
{
    var state = new AccountState(Balance: 50m, IsClosed: false);
    var result = AccountDecider.Decide(state, new AccountCommand.Deposit(25m));

    Assert.True(result.IsOk);
    Assert.Contains(result.Value, e => e is AccountEvent.Deposited(25m));
}

[Fact]
public void Withdraw_from_closed_account_fails()
{
    var state = new AccountState(Balance: 100m, IsClosed: true);
    var result = AccountDecider.Decide(state, new AccountCommand.Withdraw(10m));

    Assert.True(result.IsErr);
    Assert.IsType<AccountError.AccountClosed>(result.Error);
}

[Fact]
public void Transition_applies_deposit()
{
    var state = new AccountState(Balance: 50m, IsClosed: false);
    var (newState, _) = AccountDecider.Transition(state, new AccountEvent.Deposited(25m));

    Assert.Equal(75m, newState.Balance);
}
```

### Given-When-Then Pattern

Structure tests as event-sourced scenarios:

```csharp
[Fact]
public void Given_deposits_When_overdraw_Then_rejected()
{
    // Given: fold past events into state
    var state = new AccountState(0, false);
    state = AccountDecider.Transition(state, new AccountEvent.Deposited(100m)).Item1;
    state = AccountDecider.Transition(state, new AccountEvent.Withdrawn(80m)).Item1;
    // State: Balance = 20

    // When: issue a command
    var result = AccountDecider.Decide(state, new AccountCommand.Withdraw(50m));

    // Then: command rejected
    Assert.True(result.IsErr);
    Assert.IsType<AccountError.InsufficientFunds>(result.Error);
}
```

### Property-Based Testing

Use FsCheck or similar libraries to test invariants:

```csharp
[Property]
public bool Balance_never_goes_negative(PositiveInt[] deposits, PositiveInt[] withdrawals)
{
    var state = new AccountState(0, false);

    foreach (var d in deposits)
    {
        var result = AccountDecider.Decide(state, new AccountCommand.Deposit(d.Get));
        if (result.IsOk)
            foreach (var e in result.Value)
                state = AccountDecider.Transition(state, e).Item1;
    }

    foreach (var w in withdrawals)
    {
        var result = AccountDecider.Decide(state, new AccountCommand.Withdraw(w.Get));
        if (result.IsOk)
            foreach (var e in result.Value)
                state = AccountDecider.Transition(state, e).Item1;
    }

    return state.Balance >= 0;
}
```

## Level 2: Runner Behaviour

Test persistence, concurrency, and lifecycle using `InMemoryEventStore`:

```csharp
[Fact]
public async Task Handle_persists_events_to_store()
{
    var store = new InMemoryEventStore<CounterEvent>();
    using var runner = AggregateRunner<CounterDecider, CounterState,
        CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
        .Create(store, "test-1", default);

    await runner.Handle(new CounterCommand.Add(3));

    var stream = store.GetStream("test-1");
    Assert.Equal(3, stream.Count);
    Assert.All(stream, e => Assert.IsType<CounterEvent.Incremented>(e.Event));
}

[Fact]
public async Task Load_reconstructs_state_from_events()
{
    var store = new InMemoryEventStore<CounterEvent>();
    using var runner = AggregateRunner<CounterDecider, CounterState,
        CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
        .Create(store, "test-1", default);

    await runner.Handle(new CounterCommand.Add(5));
    runner.Dispose();

    using var loaded = await AggregateRunner<CounterDecider, CounterState,
        CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
        .Load(store, "test-1", default);

    Assert.Equal(5, loaded.State.Count);
    Assert.Equal(5, loaded.Version);
}
```

### Testing Concurrency

```csharp
[Fact]
public async Task Concurrent_handle_calls_are_serialized()
{
    var store = new InMemoryEventStore<CounterEvent>();
    using var runner = AggregateRunner<CounterDecider, CounterState,
        CounterCommand, CounterEvent, CounterEffect, CounterError, Unit>
        .Create(store, "test-1", default);

    // Fire 10 concurrent increments
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => runner.Handle(new CounterCommand.Add(1)));

    await Task.WhenAll(tasks);

    // All 10 should succeed — SemaphoreSlim serializes access
    Assert.Equal(10, runner.State.Count);
    Assert.Equal(10, runner.Version);
}
```

## Level 3: Conflict Resolution Testing

Test that `ResolveConflicts` handles merge scenarios:

```csharp
[Fact]
public void Commutative_operations_resolve_automatically()
{
    var currentState = new CounterState(5);   // after their events
    var projectedState = new CounterState(8); // after ours applied
    var ourEvents = new CounterEvent[]
    {
        new CounterEvent.Incremented(),
        new CounterEvent.Incremented(),
        new CounterEvent.Incremented()
    };
    var theirEvents = new[] { new CounterEvent.Incremented() };

    var result = CounterDecider.ResolveConflicts(
        currentState, projectedState, ourEvents, theirEvents);

    Assert.True(result.IsOk);
    Assert.Equal(ourEvents, result.Value);
}
```

## Level 4: Integration Testing

For real storage backends, use Docker containers:

```csharp
[Collection("KurrentDB")]
public class KurrentDBIntegrationTests : IAsyncLifetime
{
    private readonly KurrentDBEventStore<CounterEvent> _store;

    public KurrentDBIntegrationTests()
    {
        var client = new KurrentDBClient(
            KurrentDBClientSettings.Create("esdb://localhost:2113?tls=false"));
        _store = new KurrentDBEventStore<CounterEvent>(client, Serialize, Deserialize);
    }

    [Fact]
    public async Task Append_and_load_round_trips()
    {
        var streamId = $"test-{Guid.NewGuid()}";
        await _store.AppendAsync(streamId,
            [new CounterEvent.Incremented(), new CounterEvent.Incremented()], 0);

        var events = await _store.LoadAsync(streamId);

        Assert.Equal(2, events.Count);
    }
}
```

## What to Read Next

| If you want to… | Read |
| ---------------- | ---- |
| Implement a custom event store | [Implementing Event Stores](implementing-event-stores.md) |
| Set up KurrentDB for integration tests | [KurrentDB Setup](kurrentdb-setup.md) |
