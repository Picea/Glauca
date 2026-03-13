# Security Policy

## Supported Versions

We actively support the latest version of Picea.Glauca. Security updates are provided for:

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | ✅ Yes (pre-release) |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report security vulnerabilities by emailing: **me@mauricepeters.dev**

You should receive a response within 48 hours. If for some reason you do not, please follow up via email to ensure we received your original message.

Please include the following information in your report:

- Type of issue (e.g., event data leakage, concurrency bypass, denial of service, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

This information will help us triage your report more quickly.

## Preferred Languages

We prefer all communications to be in English.

## Security Measures

### Automated Dependency Scanning

We automatically scan for vulnerable dependencies using:

- **NuGet Audit**: Built into .NET SDK, runs on every restore (`all` mode, `low` severity)
- **GitHub Dependabot**: Monitors for security updates weekly

### Minimal Dependency Surface

Picea.Glauca's core package has a **single dependency** — the [Picea](https://github.com/picea/picea) kernel, which itself has zero external dependencies. This significantly reduces the attack surface:

| Package | Dependencies |
| ------- | ------------ |
| `Picea.Glauca` | `Picea` (kernel only, zero transitive deps) |
| `Picea.Glauca.KurrentDB` | `Picea.Glauca` + `KurrentDB.Client` |

### Concurrency Safety

All runners (`AggregateRunner`, `ResolvingAggregateRunner`, `SagaRunner`) use `SemaphoreSlim` serialization to prevent race conditions. The `InMemoryEventStore` uses `Lock` for thread-safe operations.

### Optimistic Concurrency Control

The `EventStore<TEvent>` contract enforces optimistic concurrency via version checks on every `AppendAsync`. This prevents lost-write anomalies:

- The caller provides `expectedVersion`
- The store validates it matches the actual stream version
- On mismatch, `ConcurrencyException` is thrown — no silent data loss

### Code Review

All pull requests are reviewed for:

- Secure coding practices
- Thread-safety guarantees
- Proper concurrency control
- Input validation in EventStore implementations

## Security Best Practices for Users

When using Picea.Glauca in your projects:

1. **Keep Picea.Glauca Updated**: Always use the latest version
2. **Validate Commands**: Use the Decider's `Decide` method to validate all command inputs before producing events
3. **Secure Your EventStore**: When implementing `EventStore<TEvent>`, ensure proper authentication and authorization on the backing store
4. **Protect Stream IDs**: Stream identifiers may contain domain-sensitive information — treat them as such
5. **Serialize Safely**: When using `KurrentDBEventStore`, ensure your serialization delegates handle untrusted data safely (e.g., limit deserialization types)
6. **Monitor Tracing**: Use the built-in OpenTelemetry tracing (`Picea.Glauca`, `Picea.Glauca.Saga`) to detect anomalies

## Security Updates

Security updates will be released as soon as possible after a vulnerability is confirmed. We will:

1. Publish a GitHub Security Advisory
2. Release a patch version
3. Update this document with mitigation steps if immediate patching is not possible
4. Notify users through GitHub releases and repository notifications

## Acknowledgments

We thank security researchers who responsibly disclose vulnerabilities to us. With your permission, we will acknowledge your contribution in our release notes.

## Contact

For security-related questions that are not vulnerability reports, please open a GitHub discussion or issue.

---

*Last Updated: March 13, 2026*
