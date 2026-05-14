# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Pull request template, structured issue templates (bug / feature), and `config.yml` routing security and MCP-spec questions to the right venues.
- Conventional Commits PR title linter.
- Pre-commit hooks (`prek`): pre-commit-hooks v5, markdownlint, codespell, plus a local CSharpier check.
- CSharpier formatter pinned via `.config/dotnet-tools.json`, with a project-wide format pass and `.git-blame-ignore-revs` skipping it.
- CI workflow with `lint` (CSharpier check + warnings-as-errors build) and `build` (frontend + .NET publish) jobs.

### Changed

- `Properties/launchSettings.json` UTF-8 BOM stripped so `check-json` accepts it.
- `src/js/datepicker.js` — "occured" → "occurred" comment typo fix.
- `README.md` — documented default admin credentials (`admin@mcpmanager.local` / `123456`) and reordered Getting Started so Docker appears before Run Locally (closes #2).

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

[Unreleased]: https://github.com/daniel3303/McpManager/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/daniel3303/McpManager/releases/tag/v1.0.0
