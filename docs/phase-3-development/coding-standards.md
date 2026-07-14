# Coding Standards & Style Guide

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Consistent code is easier to read, review, and maintain. Where possible, these
rules are **enforced by tooling** rather than manual review.

## 1. Languages & Tooling

**Languages:** C# (`net8.0`), Blazor WebAssembly, T-SQL (stored procedures),
PowerShell (build/deploy scripts).

| Concern | Tool | Config |
|---------|------|--------|
| Formatting | `dotnet format` (Roslyn/SDK rules) | Default C# conventions |
| Linting | Built-in Roslyn analyzers + compiler warnings | `.csproj` per project |
| Type checking | Nullable reference types | `<Nullable>enable</Nullable>` |
| SDK pinning | .NET SDK | [`global.json`](../../global.json) (`9.0.308`, roll-forward `latestMinor`) |
| CI gate | `dotnet build` + `dotnet test` | See [CI/CD](../phase-5-deployment/ci-cd.md) |

All projects enable `<Nullable>` and `<ImplicitUsings>`. Run everything locally
before pushing:

```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

## 2. Formatting

- Indentation: 4 spaces, no tabs.
- Max line length: ~120 (soft limit).
- Use **file-scoped namespaces** (`namespace Foo;`).
- `using` directives outside the namespace; `System.*` first.
- One public type per file; filename matches the type name.
- Let `dotnet format` win — don't hand-format.

## 3. Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Types / records / enums | `PascalCase` | `ComputersController` |
| Interfaces | `I` + `PascalCase` | `IComputerRepository` |
| Methods / properties / public members | `PascalCase` | `GetById` |
| Local variables / parameters | `camelCase` | `cancellationToken` |
| Private fields | `_camelCase` | `_computerRepository` |
| Constants | `PascalCase` | `Heartbeat` |
| Async library/repository methods | `PascalCase` + `Async` | `GetByIdAsync` |
| Files | `PascalCase` matching type | `ComputersController.cs` |
| Database tables / columns | `snake_case` | `computer_agent_statuses` |

> MVC controller actions omit the `Async` suffix (e.g. `GetAll`); repository and
> service methods keep it (e.g. `GetAllAsync`).

## 4. Code Organization

- Follow **Clean Architecture** layering — dependencies point inward:
  `Core` (entities/interfaces) ← `Infrastructure` (EF Core/repositories) ← `Api`;
  `Contracts` holds shared DTOs; `Web` is the Blazor client. `Core` has no
  external dependencies.
- One public type per file; keep methods small and single-purpose.
- Keep controllers thin — put business logic in services/repositories.
- Data access goes through repository interfaces backed by stored procedures.
- Prefer composition and dependency injection over inheritance.
- Group by feature within each layer.

## 5. Comments & Documentation

- Explain **why**, not **what**.
- Add XML doc comments (`///`) to public APIs, services, and DTOs.
- Annotate controller actions with `[ProducesResponseType]` for OpenAPI/Swagger.
- Remove commented-out code before merging.

## 6. Error Handling

- Validate inputs at boundaries with FluentValidation + DataAnnotations.
- Global exception middleware returns `ProblemDetails`; never leak stack traces.
- Never swallow exceptions; log with Serilog (structured) + correlation id.
- Flow `CancellationToken` through all async I/O.
- Return appropriate `ActionResult` / HTTP status codes.

## 7. Testing Expectations

- Tests use **xUnit**; new code ships with tests.
- Integration tests run against real SQL Server via `Testcontainers.MsSql`.
- Collect coverage with `coverlet`; target 80% on changed lines.
- See the [Test Plan](../phase-4-testing/test-plan.md).

## 8. Security Practices

- No secrets in code, logs, or committed `appsettings`; use env vars / user-secrets.
- Use parameterized queries and stored procedures; never concatenate SQL.
- Hash passwords with BCrypt; authenticate with JWT; enforce authorization policies
  (`AdminOnly`, `TechnicianOrAdmin`, `AllUsers`).
- Validate and sanitize all external input (see [OWASP Top 10](https://owasp.org/www-project-top-ten/)).
- Rely on security-headers middleware, HTTPS redirection, and rate limiting.

## 9. Dependencies

- Justify new NuGet packages; prefer well-maintained, widely-used libraries.
- Pin exact versions and keep them aligned across projects (`8.0.x`).
- The SDK version is pinned via [`global.json`](../../global.json).
- Review license compatibility (see [Compliance](../security/compliance.md)).

## 10. Commit & PR Hygiene

- Small, focused commits using Conventional Commits.
- PRs must pass `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes`.
- Follow the [Git Workflow](git-workflow.md).
