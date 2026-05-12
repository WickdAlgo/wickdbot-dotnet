# WickdBot DocFX Policy

Use this reference when creating or changing DocFX docs, XML documentation generation, or documentation publishing.

## Current Repo Facts

- App project: `src/WickdBot/WickdBot.csproj`.
- Test project: `tests/WickdBot.Tests/WickdBot.Tests.csproj`.
- Target framework: `net8.0`.
- WickdBot uses internal module boundaries, so public-only API docs may miss important implementation contracts.
- No DocFX config exists until this skill is applied.

## Tooling

- Prefer a repo-local tool manifest:
  - `dotnet new tool-manifest`
  - `dotnet tool install docfx`
  - Later runs: `dotnet tool restore`
- Use `dotnet docfx` or `dotnet tool run docfx -- ...` from the repository root after the tool manifest exists.
- Avoid requiring a global DocFX install for CI or future agents.

## Project Configuration

- Add `GenerateDocumentationFile=true` to `src/WickdBot/WickdBot.csproj` before expecting XML comments to drive API pages.
- Do not add a permanent global `NoWarn` for missing XML docs if the project goal is complete source documentation.
- If the project is mid-migration and warnings block progress, use a temporary documented suppression with a follow-up task.

## DocFX Configuration

- Put DocFX files under `docs/`.
- Use `docs/docfx.json` as the root config.
- Because `docfx.json` lives under `docs/`, use the metadata `src` property to point back to the repository root.
- Prefer project metadata generation from `src/WickdBot/WickdBot.csproj`.
- If project metadata generation fails because of MSBuild workspace issues, build `src/WickdBot/WickdBot.csproj` in Release and switch metadata generation to the compiled assembly plus side-by-side XML docs.

## Internal API Docs

- For internal engineering docs, set `includePrivateMembers=true`; this includes internal and private members, so filter or exclude noisy implementation details deliberately.
- Use `filterConfig.yml` for broad namespace/type filters.
- Use `<exclude />` in XML comments for one-off members that should not appear.
- Keep tests out of the default docs source.

## Validation

Run these from the repository root when practical:

- `dotnet tool restore`
- `dotnet restore src/WickdBot/WickdBot.csproj`
- `dotnet build src/WickdBot/WickdBot.csproj --configuration Release --no-restore`
- `dotnet docfx metadata docs/docfx.json --logLevel verbose`
- `dotnet docfx build docs/docfx.json --warningsAsErrors`
- `dotnet docfx serve docs/_site --port 8080`

If `dotnet docfx` is unavailable, use `dotnet tool run docfx -- <args>`.

## GitHub Pages

- Use `assets/docfx-pages.yml` as a starting point only after basic local docs build succeeds.
- Pages publishing requires repository Pages source configured for GitHub Actions.
- Use least-privilege permissions:
  - build job: `contents: read`;
  - deploy job: `pages: write` and `id-token: write`.
- Deploy only from protected/default branch pushes or manual dispatch.
- Build docs on pull requests, but do not deploy PR docs unless the user explicitly asks for previews.

## Official References To Recheck

- DocFX quick start: https://dotnet.github.io/docfx/
- DocFX .NET API docs: https://dotnet.github.io/docfx/docs/dotnet-api-docs.html
- DocFX basic concepts: https://dotnet.github.io/docfx/docs/basic-concepts.html
- DocFX config: https://dotnet.github.io/docfx/docs/config.html
- DocFX metadata command: https://dotnet.github.io/docfx/reference/docfx-cli-reference/docfx-metadata.html
- DocFX serve command: https://dotnet.github.io/docfx/reference/docfx-cli-reference/docfx-serve.html
- GitHub Pages custom workflows: https://docs.github.com/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages
