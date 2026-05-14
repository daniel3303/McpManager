# Contributing to McpManager

Thanks for considering a contribution. This guide covers the setup and conventions used across the project.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned by `global.json`).
- [Node.js 20+](https://nodejs.org/) and `npm` for the Portal frontend.
- [prek](https://github.com/j178/prek) for pre-commit hooks: `brew install prek` (or use `pre-commit` if you prefer Python — see `.pre-commit-config.yaml`).

## First-time setup

```bash
# 1. Restore .NET local tools (CSharpier, etc.)
dotnet tool restore

# 2. Install Git pre-commit hooks
prek install -f

# 3. Verify your environment by running everything once
prek run --all-files
dotnet build McpManager.sln -c Release -warnaserror
cd src/McpManager.Web.Portal && npm ci && npx vite build
```

## Branching

Use Conventional Commits prefixes for branch names too — they slot straight into the PR-title linter:

- `feat/<short-description>` — new feature.
- `fix/<short-description>` — bug fix.
- `chore/<short-description>` — tooling / repo hygiene.
- `docs/<short-description>` — docs only.
- `ci/<short-description>` — CI / workflows.
- `refactor/<short-description>` — internal restructure, no behaviour change.

Branch from `main`, push your branch, open a PR against `main`.

## Commit & PR style

- **Conventional Commits required** on PR titles — the squash-merge subject is taken from the PR title and the `lint-pr-title` workflow rejects non-compliant titles.
- Keep PRs focused. One logical change per PR. Use `git mv` for renames so history is preserved.
- Fill out the PR template — summary, code changes, verification steps, checklist.
- Update `CHANGELOG.md` under `## [Unreleased]` for any user-visible change.

## Day-to-day commands

```bash
# Build the solution (warnings-as-errors matches CI)
dotnet build McpManager.sln -c Release -warnaserror

# Format C# code
dotnet csharpier format .
dotnet csharpier check .   # CI runs this

# Frontend
cd src/McpManager.Web.Portal && npm ci && npx vite build

# Run the Portal locally (check launchSettings.json for ports)
dotnet run --project src/McpManager.Web.Portal

# Add an EF Core migration
dotnet ef migrations add <Name> \
    --project src/McpManager.Core.Data \
    --startup-project src/McpManager.Web.Portal

# Run all pre-commit hooks against the whole repo
prek run --all-files
```

## Filing issues

- **Bug reports** → use the bug template; include version, host, and reproduction steps.
- **Feature requests** → use the feature template; lead with the problem, not the solution.
- **Security vulnerabilities** → see [SECURITY.md](SECURITY.md); report privately via GitHub advisories, **not** as a public issue.

## Code of conduct

This project follows the [Contributor Covenant v2.1](CODE_OF_CONDUCT.md). By participating you agree to its terms.
