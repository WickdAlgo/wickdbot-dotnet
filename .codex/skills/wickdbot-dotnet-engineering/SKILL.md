---
name: wickdbot-dotnet-engineering
description: Apply durable .NET engineering practices in the WickdBot repository. Use when implementing, refactoring, reviewing, testing, documenting, or planning C#/.NET changes in WickdBot, especially work touching Program.cs, *.csproj, solution structure, dependency management, configuration, logging, XML documentation comments, async/disposal behavior, tests, analyzers, CI, or long-term maintainability.
---

# WickdBot .NET Engineering

## Purpose

Keep WickdBot changes small, repo-compatible, and easy to evolve. Prefer evidence from the current project over generic .NET advice, and add engineering structure only when it solves a real maintenance problem.

## Workflow

1. Inspect the repo shape before deciding: solution files, target frameworks, SDK settings, package references, tests, analyzers, CI, and existing code style.
2. Classify the work as behavior, architecture, tooling, dependency, test, or cleanup. Let that classification decide the blast radius.
3. Preserve the current style for narrow edits. Propose a baseline change only when it improves future work or the user asked for long-term cleanup.
4. For behavior changes, add or update tests unless the repo has no test project and the change is too small to justify creating the first one. If creating the first test project, choose one framework deliberately and keep it documented.
5. Validate with the repository's actual commands. Prefer the solution file when the installed SDK supports it; otherwise use `dotnet build .\WickdBot.csproj`. Run `dotnet test` when tests exist; run formatting/analyzer checks when configured or when the change touches style/tooling.
6. Report what changed, how it was verified, and any remaining risk.

## Current Baseline

- Treat WickdBot as a `net8.0` console application unless the project files change.
- Keep `ImplicitUsings=enable` behavior in mind and avoid adding redundant usings.
- Do not introduce hosting, dependency injection, configuration, logging, analyzers, packages, or a test framework just because they are common in larger .NET apps. Add them when the feature or maintenance goal needs them.
- Avoid churn in `Properties/AssemblyInfo.cs` and project metadata unless versioning, packaging, or assembly attributes are part of the task.

## Engineering Rules

- Prefer simple, explicit C# over pattern-heavy abstractions. Add interfaces, factories, handlers, or service layers only when they decouple real dependencies or make tests clearer.
- Keep async async all the way for I/O. Accept `CancellationToken` on long-running or externally triggered operations; avoid `.Result`, `.Wait()`, and fire-and-forget work unless the lifetime is owned and observed.
- Dispose resources deterministically with `using` or `await using`; avoid hiding ownership across broad static state.
- Keep dependency updates intentional. Check package purpose, version compatibility, transitive impact, and whether the BCL or existing project can do the job.
- Treat error handling as part of the contract. Use specific exceptions for programmer errors, explicit return/result shapes for expected domain failures, and structured logging once logging infrastructure exists.
- Put C# XML documentation in every non-generated `.cs` source file. Document each declared type and every public or internal member that forms a module contract; keep private implementation comments sparse and only where clarity needs them.
- When making non-trivial architecture, testing, quality, dependency, or documentation decisions, read `references/engineering-standards.md`.

## External .NET Skills

For broad or specialized .NET work, prefer modular .NET catalog skills over a single generic best-practices skill. Useful future candidates are `dotnet`, `project-setup`, `modern-csharp`, `format`, `code-analysis`, `code-review`, `architecture`, and focused testing bundles. Do not install global tools or external skills without user approval.
