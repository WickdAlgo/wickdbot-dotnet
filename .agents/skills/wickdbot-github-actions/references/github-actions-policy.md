# WickdBot GitHub Actions Policy

Use this reference before creating or changing `.github/workflows/*.yml` or `.github/workflows/*.yaml`.

## Current Repo Facts

- App project: `src/WickdBot/WickdBot.csproj`.
- Test project: `tests/WickdBot.Tests/WickdBot.Tests.csproj`.
- Target framework: `net8.0`.
- Test framework: xUnit.
- Default CI runner: `ubuntu-latest`.
- Default .NET SDK channel: `8.0.x`.
- Do not assume `WickdBot.slnx` is usable in CI. Prefer project paths unless SDK support is confirmed.

## Initial CI

- Create `.github/workflows/ci.yml`.
- Trigger on `pull_request`, `push` to the main branch, and `workflow_dispatch`.
- Use `permissions: contents: read`.
- Use `concurrency` keyed by workflow and ref.
- Restore, build, and test the test project because it references the app project.
- Use `Release` configuration in CI.
- Upload TRX and coverage outputs when present so failures can be inspected.

## Caching

- Do not enable `actions/setup-dotnet` cache unless `packages.lock.json` files exist or the change intentionally enables locked restore.
- If locked restore is adopted, commit lock files and use `dotnet restore --locked-mode` in CI.
- Avoid saving caches from workflows that execute untrusted PR code with elevated permissions.

## Security

- Use `pull_request`, not `pull_request_target`, for build and test jobs that run PR code.
- Use top-level `permissions: contents: read` for ordinary CI.
- Add write permissions only to the specific job that needs them.
- Never use `write-all`.
- Never put secrets in workflow files or logs.
- Do not expose secrets to workflows that run untrusted fork code.
- Treat GitHub event text as untrusted input. Pass it through environment variables before shell use.
- Prefer official GitHub actions. For third-party actions, verify maintenance, reputation, and permissions. Consider SHA pinning for high-risk actions.

## CD Requirements

Do not add CD until the deployment target is explicit. Ask or infer only when the repo contains clear deployment artifacts.

When adding CD:

- Use separate jobs or workflows for deploys.
- Trigger deploys from tags, releases, or protected branches.
- Use GitHub environments with required reviewers for production.
- Prefer OIDC for cloud authentication.
- Declare the narrowest possible permissions, for example `id-token: write` only for an OIDC deployment job.
- List every required secret, variable, environment, package permission, and branch protection requirement in the final response.

## Recommended Optional Workflows

- `codeql.yml` only when the user wants code scanning or the repo is ready for security checks.
- `dependency-review.yml` for PR dependency changes when the repository is public or GitHub Advanced Security/dependency review is available.
- `release.yml` only after versioning, artifacts, and release target are defined.

## Validation

- Run the same local commands the workflow uses when practical:
  - `dotnet restore tests/WickdBot.Tests/WickdBot.Tests.csproj`
  - `dotnet build tests/WickdBot.Tests/WickdBot.Tests.csproj --configuration Release --no-restore`
  - `dotnet test tests/WickdBot.Tests/WickdBot.Tests.csproj --configuration Release --no-build`
- Lint workflow YAML with `actionlint` if available.
- If `gh` is authenticated and the workflow exists on GitHub, use `gh workflow list` and `gh run list` for remote verification.

## Official References To Recheck

- Workflow syntax: https://docs.github.com/actions/reference/workflows-and-actions/workflow-syntax
- Events: https://docs.github.com/actions/reference/events-that-trigger-workflows
- GITHUB_TOKEN permissions: https://docs.github.com/actions/concepts/security/github_token
- setup-dotnet: https://github.com/actions/setup-dotnet
- upload-artifact: https://github.com/actions/upload-artifact
- cache: https://github.com/actions/cache
