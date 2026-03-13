# Tutorial 01: Your First Aggregate

Build a bank account aggregate with deposits, withdrawals, and overdraft protection.

## What You'll Learn

- Define a `Decider` with state, commands, events, effects, and errors
- Use `AggregateRunner` to persist events and handle commands
- Load an aggregate from an existing event stream
- Test the domain logic directly (no runner needed)

## Prerequisites

```bash
dotnet new console -n BankAccount
cd BankAccount
dotnet add package Picea.Glauca
```

## Step 1: Define the Domain Types

```csharp
using Picea;
using Picea.Glauca;

// State
public readonly record struct AccountState(decimal Balance, bool IsClosed);

// Commands
public interface AccountCommand
{
    record struct Deposit(decimal Amount) : AccountCommand;
    record struct Withdraw(decimal Amount) : AccountCommand;
    record struct Close : AccountCommand;
}

// Events
public interface AccountEvent
{
    record struct Deposited(decimal Amount) : AccountEvent;
    record struct Withdrawn(decimal Amount) : AccountEvent;
    record struct Closed : AccountEvent;
}

// Effects
public interface AccountEffect
{
    record struct None : AccountEffect;
    record struct NotifyOwner(string Message) : AccountEffect;
}

// Errors
public interface AccountError
{
    record struct InsufficientFunds(decimal Balance, decimal Requested) : AccountError;
    record struct AccountClosed : AccountError;
    record struct InvalidAmount(decimal Amount) : AccountError;
}
```

Each concept is a simple C# type. Commands are imperative ("do this"), events are past tense ("this happened"), errors explain "why not."

## Step 2: Implement the Decider

```csharp
public class AccountDecider
    : Decider<AccountState, AccountCommand, AccountEvent, AccountEffect, AccountError, Unit>
{
    public static (AccountState, AccountEffect) Initialize(Unit _) =>
        (new AccountState(Balance: 0m, IsClosed: false), new AccountEffect.None());

    public static Result<AccountEvent[], AccountError> Decide(
        AccountState state, AccountCommand command) =>
        command switch
        {
            _ when state.IsClosed =>
                Result<AccountEvent[], AccountError>
                    .Err(new AccountError.AccountClosed()),

            AccountCommand.Deposit(var amount) when amount <= 0 =>
                Result<AccountEvent[], AccountError>
                    .Err(new AccountError.InvalidAmount(amount)),

            AccountCommand.Deposit(var amount) =>
                Result<AccountEvent[], AccountError>
                    .Ok([new AccountEvent.Deposited(amount)]),

            AccountCommand.Withdraw(var amount) when amount <= 0 =>
                Result<AccountEvent[], AccountError>
                    .Err(new AccountError.InvalidAmount(amount)),

            AccountCommand.Withdraw(var amount) when amount > state.Balance =>
                Result<AccountEvent[], AccountError>
                    .Err(new AccountError.InsufficientFunds(state.Balance, amount)),

            AccountCommand.Withdraw(var amount) =>
                Result<AccountEvent[], AccountError>
                    .Ok([new AccountEvent.Withdrawn(amount)]),

            AccountCommand.Close =>
                Result<AccountEvent[], AccountError>
                    .Ok([new AccountEvent.Closed()]),

            _ => throw new UnreachableException()
        };

    public static (AccountState, AccountEffect) Transition(
        AccountState state, AccountEvent @event) =>
        @event switch
        {
            AccountEvent.Deposited(var amount) =>
                (state with { Balance = state.Balance + amount }, new AccountEffect.None()),

            AccountEvent.Withdrawn(var amount) =>
                (state with { Balance = state.Balance - amount }, new AccountEffect.None()),

            AccountEvent.Closed =>
                (state with { IsClosed = true },
                    new AccountEffect.NotifyOwner("Your account has been closed")),

            _ => throw new UnreachableException()
        };
}
```

Notice: `Decide` and `Transition` are **pure functions**. No database calls, no I/O, no dependencies. They're just pattern matching on data.

## Step 3: Run with AggregateRunner

```csharp
// Create an in-memory event store
var store = new InMemoryEventStore<AccountEvent>();

// Create a new aggregate
using var account = AggregateRunner<AccountDecider, AccountState,
    AccountCommand, AccountEvent, AccountEffect, AccountError, Unit>
    .Create(store, streamId: "account-001", parameters: default);

// Deposit $100
var result = await account.Handle(new AccountCommand.Deposit(100m));
Console.WriteLine($"Balance: {account.State.Balance}"); // Balance: 100

// Withdraw $30
result = await account.Handle(new AccountCommand.Withdraw(30m));
Console.WriteLine($"Balance: {account.State.Balance}"); // Balance: 70

// Try to overdraw — domain rejects it
result = await account.Handle(new AccountCommand.Withdraw(200m));
Console.WriteLine($"Rejected: {result.IsErr}"); // Rejected: True

// Check version — 2 successful commands produced 2 events
Console.WriteLine($"Version: {account.Version}"); // Version: 2
```

## Step 4: Load from an Existing Stream

```csharp
// Later — load the aggregate from the same stream
using var loaded = await AggregateRunner<AccountDecider, AccountState,
    AccountCommand, AccountEvent, AccountEffect, AccountError, Unit>
    .Load(store, streamId: "account-001", parameters: default);

Console.WriteLine($"Balance: {loaded.State.Balance}");  // Balance: 70
Console.WriteLine($"Version: {loaded.Version}");         // Version: 2
```

`Load` replays all events through `Transition` to reconstruct the current state. No snapshots needed — the fold is the source of truth.

## Step 5: Test Without a Runner

The best part: test your domain logic directly.

```csharp
// Test Decide
var state = new AccountState(Balance: 50m, IsClosed: false);
var decision = AccountDecider.Decide(state, new AccountCommand.Withdraw(100m));
Debug.Assert(decision.IsErr);
Debug.Assert(decision.Error is AccountError.InsufficientFunds);

// Test Transition
var (newState, effect) = AccountDecider.Transition(
    new AccountState(Balance: 50m, IsClosed: false),
    new AccountEvent.Deposited(25m));
Debug.Assert(newState.Balance == 75m);

// Test invariant: closed account rejects all commands
var closedState = new AccountState(Balance: 0m, IsClosed: true);
var closedDecision = AccountDecider.Decide(closedState, new AccountCommand.Deposit(10m));
Debug.Assert(closedDecision.IsErr);
Debug.Assert(closedDecision.Error is AccountError.AccountClosed);
```

No mocking. No async. No runner. Just pure function calls.

## What You Built

```text
AccountCommand ─── Decide ───▶ AccountEvent[] ─── Transition ───▶ AccountState
                    │                                 │
                    │ pure                            │ pure
                    │                                 │
              AggregateRunner                   AggregateRunner
              (persistence +                   (state update +
               concurrency)                    effect capture)
```

## What's Next

| If you want to… | Read |
| ---------------- | ---- |
| Build read models | [Tutorial 02: Projections](02-projections.md) |
| Handle concurrent writes | [Tutorial 03: Conflict Resolution](03-conflict-resolution.md) |
| Coordinate across aggregates | [Tutorial 04: Sagas](04-sagas.md) |
| Use a real database | [KurrentDB Setup Guide](../guides/kurrentdb-setup.md) |
