# Contributing to Picea.Glauca

Thank you for your interest in contributing to Picea.Glauca! This document provides guidelines for contributing to the event sourcing library.

## 🌳 Trunk-Based Development

We follow **trunk-based development** practices:

- **Main branch** (`main`) is always deployable and protected
- All changes go through **pull requests**
- Feature branches are **short-lived** (< 2 days)
- Commits are **small and frequent**
- CI/CD validates all changes before merge

## 🔒 Branch Protection Rules

The `main` branch is protected with the following rules:

### Required Status Checks
All PRs must pass:
- ✅ **Build & Test** — `dotnet build`, `dotnet test`
- ✅ **Format** — `dotnet format --verify-no-changes`

### Pull Request Requirements
- ✅ **At least 1 approval** required
- ✅ **Up-to-date branches** — Must be current with main before merge
- ✅ **Conversation resolution** — All comments must be resolved
- ❌ **No force pushes** allowed
- ❌ **No branch deletions** allowed

### Additional Protections
- Administrators **must follow these rules** (no bypass)
- **Linear history** enforced (squash or rebase merging only)

## 🚀 Workflow

### 1. Create a Feature Branch

```bash
# Always start from the latest main
git checkout main
git pull origin main

# Create a short-lived feature branch
git checkout -b feature/your-feature-name
# or
git checkout -b fix/issue-description
```

### 2. Make Small, Incremental Changes

- Keep commits focused and atomic
- Write descriptive commit messages
- Commit frequently (multiple times per day)
- Follow [Conventional Commits](https://www.conventionalcommits.org/) format:

```
feat: add snapshot support to AggregateRunner
fix: resolve race condition in SagaRunner terminal check
docs: update projection guide with catch-up example
test: add concurrency conflict resolution tests
refactor: simplify ResolvingAggregateRunner retry loop
perf: use PoolingAsyncValueTaskMethodBuilder on hot paths
```

### 3. Keep Your Branch Up-to-Date

```bash
# Regularly sync with main
git fetch origin
git rebase origin/main
```

### 4. Run Tests Locally

Before pushing, ensure all tests pass:

```bash
# Build (treat warnings as errors)
dotnet build --warnaserror

# Run core tests
dotnet test Picea.Glauca.Tests

# Run KurrentDB integration tests (requires KurrentDB running)
dotnet test Picea.Glauca.KurrentDB.Tests

# Verify formatting
dotnet format --verify-no-changes
```

### 5. Push and Create Pull Request

```bash
# Push your branch
git push origin feature/your-feature-name

# Create PR via GitHub UI or CLI
gh pr create --title "feat: your feature description" --body "Description of changes"
```

### 6. Merge

Once approved and all checks pass:
- Use **Squash and Merge** (preferred) — Creates clean history
- Or **Rebase and Merge** — Preserves individual commits
- ❌ **Never use regular merge** — Creates messy history

## 📝 Pull Request Guidelines

### PR Title

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

```
feat: add snapshot support to AggregateRunner
fix: resolve race condition in SagaRunner terminal check
docs: update projection guide with catch-up example
test: add concurrency conflict resolution tests
```

### PR Description

Include:
- **What** — What changes are being made
- **Why** — Why these changes are needed
- **How** — How the changes work
- **Testing** — What testing was performed
- **Related Issues** — Link to any related issues

### PR Size
- Keep PRs **small** (< 400 lines changed)
- Break large features into multiple PRs
- Each PR should be independently reviewable

## 🧪 Testing Requirements

### Unit Tests
- All new public APIs must have unit tests
- Aim for high code coverage (> 80%)
- Tests should be fast (< 1s per test)
- Use xUnit with `[Fact]` and `[Theory]`
- Use `InMemoryEventStore<TEvent>` for unit tests — it's included for exactly this purpose

### Integration Tests
- EventStore adapter implementations need integration tests against the real database
- See `Picea.Glauca.KurrentDB.Tests` for the pattern

### Testing Philosophy

The Picea ecosystem's core design principle — **pure transition functions** — makes testing straightforward:

1. **Test the Decider directly** — `Decide` and `Transition` are pure functions. No mocking, no async, no I/O.
2. **Test the Runner** — Use `InMemoryEventStore` to verify persistence, concurrency, and lifecycle behavior.
3. **Test the EventStore adapter** — Integration test against the real database.

## 📚 Code Style

### C# Guidelines
- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use pure functions where possible
- Prefer immutability (records, readonly structs)
- Use pattern matching
- Prefer `Result<T, E>` over exceptions for expected failures (domain errors)
- Use exceptions for infrastructure failures (concurrency, I/O)
- Enable nullable reference types

### Architecture Principles
- **Minimal dependencies** — The core package depends only on `Picea`
- **Mealy machine semantics** — All runners execute `Transition` as a pure fold
- **Separation of concerns** — Pure domain logic (Decider) vs. persistence infrastructure (EventStore)
- **Strategy via delegates** — Serialization in `KurrentDBEventStore` uses delegates, not interfaces

### Performance Conventions
- Use `ValueTask` for async returns (not `Task`)
- Apply `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` on hot async paths
- Use `readonly record struct` for value objects (`StoredEvent`, `ConflictNotResolved`)

## 🐛 Bug Reports

When reporting bugs, include:
- Clear description of the issue
- Steps to reproduce
- Expected vs actual behavior
- .NET version and OS
- EventStore implementation used (InMemory, KurrentDB, custom)
- Minimal code sample

## 💡 Feature Requests

When requesting features:
- Check existing issues first
- Describe the use case
- Explain whether it belongs in the core package or an adapter package
- Consider how it interacts with the `EventStore<TEvent>` abstraction

## 🔐 Security

- Review [SECURITY.md](SECURITY.md) for security policies
- Never commit secrets or API keys
- Report security vulnerabilities privately to me@mauricepeters.dev

## 📜 License

By contributing, you agree that your contributions will be licensed under the same [Apache 2.0 License](LICENSE) that covers this project.

---

Thank you for contributing to Picea.Glauca! 🌲
