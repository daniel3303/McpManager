# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

## [1.1.2] — 2026-05-16

### Fixed

- OpenAPI path and query arguments are now serialized in canonical JSON form. Boolean values were emitted as .NET `True`/`False` instead of `true`/`false`, causing upstream OpenAPI servers to reject calls to OpenAPI-backed tools (GH-330).
- OpenAPI enum values now preserve their JSON kind. Integer enum values were emitted as JSON strings in the generated tool schema, leading clients to send the wrong types (GH-328).
- `McpServerManager.ValidateServer` now validates auth completeness for the OpenApi transport, so a server with incomplete auth configuration can no longer be saved and silently fail at call time (GH-332).
- Namespace slug validation now rejects a trailing newline; the regex `$` anchor previously accepted a slug ending in `\n` (GH-334).

## [1.1.1] — 2026-05-15

### Fixed

- Dockerfile installs Node 22 via NodeSource in both base and build stages. The Debian apt default (Node 18) is too old for Vite 8 / Tailwind 4 / rolldown — the v1.1.0 docker-publish run failed during `npm ci && npm run build` because of this. CI on GitHub-hosted runners has Node 20+ in PATH so the regression was invisible until the tag fired the Docker workflow. The `daniel3303/mcpmanager:1.1.0` image was never published; this is the first 1.1.x with a working Docker artifact.

## [1.1.0] — 2026-05-15

### Added

- xUnit v3 unit and integration test projects (`tests/McpManager.UnitTests`, `tests/McpManager.IntegrationTests`) with 25+ tests pinning controller flows, identity, antiforgery, MCP proxy behaviors, and JSON wire formats.
- CodeQL analysis for C# and JavaScript/TypeScript, with `.github/codeql/codeql-config.yml` excluding generated Razor / EF code.
- Codecov coverage upload from CI; Codecov and CodeQL badges in README.
- Dependabot config for NuGet, npm, GitHub Actions, and Docker.
- CODEOWNERS, FUNDING.yml, CONTRIBUTING.md, CHANGELOG.md, CODE_OF_CONDUCT.md, SECURITY.md.
- Pull request template, structured issue templates (bug / feature), and `config.yml` routing security and MCP-spec questions to the right venues.
- Conventional Commits PR title linter (`lint-pr-title` workflow).
- Pre-commit hooks (`prek`): pre-commit-hooks v5, markdownlint, codespell, plus a local CSharpier check.
- CSharpier formatter pinned via `.config/dotnet-tools.json`, with a project-wide format pass and `.git-blame-ignore-revs` skipping it.
- CI workflow with `lint` (CSharpier check + warnings-as-errors build) and `build-and-test` (frontend build + .NET test + publish) jobs.

### Changed

- Projects relocated under `src/` and tests under `tests/`; solution file, `coverage.runsettings`, and `codecov.yml` stay at the repo root.
- Login POST now validates the antiforgery token (`[ValidateAntiForgeryToken]`), closing a CSRF gap on the form-based login flow.
- `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` bumped 10.0.3 → 10.0.8.
- `Microsoft.Bcl.Memory` pinned to 10.0.8 in `McpManager.Web.Portal` to clear advisory GHSA-73j8-2gch-69rq introduced transitively by `Microsoft.ML.Tokenizers`.
- AngleSharp 1.3.0 → 1.4.0 and AwesomeAssertions 9.2.0 → 9.4.0 in test projects.
- `Properties/launchSettings.json` UTF-8 BOM stripped so `check-json` accepts it.
- `src/js/datepicker.js` — corrected a comment misspelling of "occurred".
- `README.md` — documented default admin credentials (`admin@mcpmanager.local` / `123456`) and reordered Getting Started so Docker appears before Run Locally (closes #2).

### Fixed

- User profile edit no longer silently sets `EmailConfirmed = false`. Combined with `SignIn.RequireConfirmedEmail = true`, the missing form inputs were locking the edited user out on every save (closes #1).

## [1.0.0] — 2026-02-28

Initial public release.

### Added

- MCP proxy / aggregation platform exposing a unified `/mcp` endpoint over multiple upstream MCP servers.
- Dual-transport MCP client: HTTP (Bearer / Basic / ApiKey auth) and Stdio (CLI command + env vars).
- Namespaced proxy server allowing per-namespace tool grouping.
- Tool sync, customisation, and execution across registered upstream servers.
- OpenAPI-to-MCP support — register an OpenAPI spec as if it were an MCP server.
- ASP.NET Identity-backed authentication with user management, roles, and password flows.
- API key management with scoping and per-key rate limiting.
- Health checks and notifications for upstream MCP servers.
- Live request logging and streaming view.
- Interactive MCP Playground in the Portal.
- Import-from-config flow for bulk MCP server registration.
- Admin dashboard built on Tailwind CSS + DaisyUI, bundled with Vite.
- SQLite-backed persistence via Entity Framework Core with auto-migration on Portal startup.
- Serilog logging to console + rolling files under `data/logs/`.
- Multi-arch Docker image (`linux/amd64`, `linux/arm64`) published to Docker Hub on tag push.

[Unreleased]: https://github.com/daniel3303/McpManager/compare/v1.1.2...HEAD
[1.1.2]: https://github.com/daniel3303/McpManager/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/daniel3303/McpManager/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/daniel3303/McpManager/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/daniel3303/McpManager/releases/tag/v1.0.0
