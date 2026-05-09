# WickdBot Engineering Standards

Use this reference when a WickdBot task affects more than a few lines, introduces dependencies, changes architecture, creates tests, or sets repository-wide policy.

## Change Shape

- Prefer a narrow, behavior-led change before refactoring.
- Separate mechanical cleanup from behavior changes when possible.
- Avoid speculative layers. Name abstractions after current domain needs, not anticipated futures.
- Keep public surface area minimal until another part of the codebase needs it.

## Project Baseline

- `WickdBot.csproj` currently targets `net8.0` and enables implicit usings.
- Consider enabling nullable reference types when there is enough code to justify the migration. Do not slip nullable enablement into an unrelated feature.
- Consider `.editorconfig`, analyzers, and CI once the repo has recurring edits or a first meaningful feature.
- Prefer solution-level commands when the installed SDK supports `WickdBot.slnx`; otherwise validate against `WickdBot.csproj`.

## C# Design

- Use modern C# where it improves clarity and the project target supports it.
- Keep constructors honest: inject dependencies only when there is a real alternative implementation, external boundary, or test seam.
- Keep static helpers pure and small. Avoid static mutable state for runtime behavior.
- Use records for immutable data carriers and classes for behavior-rich objects.
- Prefer explicit names that reflect WickdBot domain concepts over generic suffixes like `Manager`, `Helper`, or `Processor`.

## Documentation

- Every non-generated `.cs` file should contain useful C# XML documentation for the type or types it declares.
- Document all public and internal types, records, enums, interfaces, constructors, methods, properties, and events that define module contracts.
- Include `<param>`, `<returns>`, and `<exception>` tags when they add information that a caller or maintainer needs.
- Keep docs factual and behavior-focused. Do not restate the member name in prose or add filler comments.
- Update docs in the same change that updates behavior.
- Use ordinary inline comments only for non-obvious implementation details, algorithm choices, market-structure rules, or backtest assumptions.
- Generated files are exempt, but the generator or template should carry documentation when practical.

## Async, Lifetime, and Reliability

- Use async APIs for I/O and externally delayed work.
- Propagate `CancellationToken` through long-running operations, background loops, network calls, file I/O, and bot command handling.
- Avoid swallowing exceptions. Convert expected failures into explicit results or user-visible messages at the boundary.
- Own resource lifetime clearly with `using`, `await using`, or a host/container once one exists.

## Dependencies

- Add NuGet packages only when they materially reduce complexity or provide a well-maintained integration.
- Before adding a package, check whether the BCL or an existing dependency covers the need.
- Prefer Microsoft.Extensions packages when the application grows into hosting, options, logging, HTTP clients, or dependency injection.
- Keep package versions compatible with `net8.0`; avoid preview dependencies unless the task explicitly requires them.

## Testing

- If tests already exist, follow the existing framework and conventions.
- If creating the first test project, choose a single mainstream framework deliberately and keep the setup simple. Prefer xUnit for a new small .NET app unless the user or repository establishes another standard.
- Test behavior and boundaries, not implementation details.
- Add regression tests for bug fixes.
- Use deterministic fakes for clocks, random values, network calls, bot APIs, and file systems when those boundaries appear.

## Quality Gates

- For code changes, run `dotnet build .\WickdBot.slnx` when supported by the local SDK; otherwise run `dotnet build .\WickdBot.csproj`.
- When tests exist, run `dotnet test` against the supported solution or project target.
- When formatting/analyzers are configured, run their verification command before finishing.
- If a command cannot run, report the blocker and the remaining risk.

## Review Checklist

- The change solves the requested behavior without unrelated churn.
- Names and boundaries match the current WickdBot domain.
- C# XML documentation exists in each touched `.cs` file and matches the behavior.
- New dependencies are justified and compatible.
- Async, cancellation, and disposal are handled at external boundaries.
- Tests or validation match the risk of the change.
- The final response includes validation evidence.
