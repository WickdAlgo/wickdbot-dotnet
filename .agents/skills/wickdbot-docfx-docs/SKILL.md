---
name: wickdbot-docfx-docs
description: Create, review, and maintain DocFX documentation for WickdBot. Use when adding or changing docs/docfx.json, DocFX table-of-contents files, C# XML documentation generation, API documentation filters, local DocFX tooling, docs preview commands, or GitHub Pages workflows that publish WickdBot XML docs as a browsable website.
---

# WickdBot DocFX Docs

## Purpose

Turn WickdBot C# XML documentation comments into a browsable DocFX website. Optimize for internal engineering navigation first: WickdBot module boundaries are internal, so public-only API docs are usually too sparse.

## Workflow

1. Inspect the repo before changing docs: `src/WickdBot/WickdBot.csproj`, `tests/WickdBot.Tests/WickdBot.Tests.csproj`, existing `docs/`, `.config/dotnet-tools.json`, `.github/workflows/`, and `.agents/skills/wickdbot-dotnet-engineering`.
2. Prefer a repo-local DocFX tool manifest over global installs. Use `dotnet tool restore` in repeatable workflows once `.config/dotnet-tools.json` exists.
3. Ensure the app project emits XML docs with `GenerateDocumentationFile=true`. Do not hide missing-doc warnings permanently if the user wants every C# file documented.
4. Use `assets/docfx.json` as the initial DocFX config. It generates API metadata from `src/WickdBot/WickdBot.csproj` and is configured for internal engineering docs.
5. Copy the page assets into `docs/`: `index.md`, `toc.yml`, `api/index.md`, and `filterConfig.yml`.
6. Validate in stages: restore/build the project, run `dotnet docfx metadata docs/docfx.json`, run `dotnet docfx build docs/docfx.json`, then preview with `dotnet docfx serve docs/_site`.
7. For GitHub Pages publishing, use `assets/docfx-pages.yml` only after the CI skill and repo settings are considered. Pages deployment requires repository Pages source set to GitHub Actions.
8. Finish by reporting generated files, preview command or URL, validation results, and any remaining warnings from DocFX.

## WickdBot Defaults

- Use docs root: `docs/`.
- Use generated site output: `docs/_site/`.
- Use generated API metadata output: `docs/api/`.
- Use .NET SDK channel: `8.0.x`.
- Use `src/WickdBot/WickdBot.csproj` as the DocFX metadata source.
- Exclude test projects from docs unless the user explicitly asks for test API docs.
- Prefer `memberLayout=samePage` and `namespaceLayout=nested` for easier browsing.
- Include internal/private symbols for internal engineering docs, then filter or exclude noisy symbols deliberately.

## Documentation Rules

- Keep XML docs factual and behavior-focused.
- Document every non-generated `.cs` file through its declared type docs and contract-facing member docs.
- Prefer `<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`, and `<example>` where they help readers.
- Avoid markdown that renders poorly in IDE XML docs. If markup causes issues, set DocFX `shouldSkipMarkup=true` intentionally and document why.
- Use `<exclude />` for one-off members that should not appear in generated docs.

## Resources

- `assets/docfx.json`: WickdBot DocFX configuration template.
- `assets/index.md`: docs homepage template.
- `assets/toc.yml`: root table of contents template.
- `assets/api-index.md`: API landing page template, copied to `docs/api/index.md`.
- `assets/filterConfig.yml`: default internal-docs filter template.
- `assets/docfx-pages.yml`: GitHub Pages publishing workflow template.
- `references/docfx-policy.md`: detailed setup, validation, filtering, and publishing policy.
