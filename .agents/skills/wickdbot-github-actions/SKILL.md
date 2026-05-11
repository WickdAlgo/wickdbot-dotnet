---
name: wickdbot-github-actions
description: Create, review, and maintain secure GitHub Actions CI/CD workflows for the WickdBot .NET repository. Use when adding or changing .github/workflows/*.yml or *.yaml, setting up build/test CI, release or deployment automation, workflow permissions, action versions, artifacts, caching, CodeQL/security checks, branch protection readiness, or GitHub Actions troubleshooting for WickdBot.
---

# WickdBot GitHub Actions

## Purpose

Set up GitHub Actions for WickdBot with secure defaults, current official action patterns, and repo-specific .NET commands. Prefer a small CI workflow first; add CD only after the deployment target and secret model are explicit.

## Workflow

1. Inspect the repo before writing YAML: `.github/workflows`, `src/WickdBot/WickdBot.csproj`, `tests/WickdBot.Tests/WickdBot.Tests.csproj`, solution files, `global.json`, package lock files, test loggers, coverage setup, and branch names.
2. Check current official action major versions when network is available. At creation time, official docs/repos showed `actions/checkout@v6`, `actions/setup-dotnet@v5`, and `actions/upload-artifact@v7`; verify again before major workflow changes.
3. For initial CI, use `assets/dotnet-ci.yml` as the starting template and adapt only paths, branch names, .NET version, and test/artifact behavior that the repo actually needs.
4. Do not add deployment steps until the user names a target such as NuGet, GitHub Releases, GHCR/Docker, a VM, Kubernetes, or a cloud provider. Read `references/github-actions-policy.md` before adding any CD workflow.
5. Use least privilege permissions. Build/test workflows should normally use `permissions: contents: read`.
6. Avoid `pull_request_target` for workflows that check out, build, or test PR code.
7. Validate locally where possible: run the same `dotnet restore`, `dotnet build`, and `dotnet test` targets the workflow uses; lint workflow YAML with `actionlint` if available.
8. Finish by listing the workflows changed, triggers, permissions, validation run, and any required GitHub repo settings, secrets, variables, or environment protections.

## WickdBot Defaults

- Treat WickdBot as a `.NET 8` repo with app project `src/WickdBot/WickdBot.csproj` and xUnit test project `tests/WickdBot.Tests/WickdBot.Tests.csproj`.
- Prefer project-level commands unless local inspection proves the installed SDK supports the repo's solution file format.
- Use Ubuntu hosted runners for default CI unless Windows-specific behavior is being tested.
- Set `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` in workflow env for quieter logs.
- Do not enable NuGet caching unless lock files exist or the change intentionally adopts locked restore.
- Upload test results on failure or always when the workflow is intended to support PR diagnostics.

## Security Rules

- Pin to maintained major versions of official actions. For sensitive third-party actions, verify source reputation and consider SHA pinning.
- Never hardcode credentials, tokens, API keys, exchange keys, cloud credentials, or deployment endpoints.
- Keep secrets out of PR workflows that run untrusted code.
- Put write permissions only on the job that needs them.
- Use GitHub environments and required reviewers for deployments.
- Prefer OIDC for cloud deployments instead of long-lived cloud secrets.
- Pass untrusted GitHub context values through environment variables before using them in shell scripts.

## Resources

- `assets/dotnet-ci.yml`: conservative WickdBot .NET CI template.
- `references/github-actions-policy.md`: security, CI, CD, caching, artifacts, and validation policy.
