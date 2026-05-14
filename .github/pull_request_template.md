<!-- markdownlint-disable MD041 -->
## Summary

<!-- One paragraph: what changed and why. Link related issues. -->

## Code changes

<!-- Concise bullet list of the files / areas touched and what each change does. -->

## Verification

<!-- How you confirmed this works. Include commands run and outcomes. Examples:
  - `dotnet build -c Release` — passes with 0 warnings, 0 errors
  - Smoke-tested in browser at /servers — sync, list, delete all work
  - Migration applied locally on a copy of the prod DB — no drift -->

## Checklist

- [ ] Tests pass locally (`dotnet build` and any relevant manual smoke tests).
- [ ] Code formatted (`dotnet csharpier format .` clean).
- [ ] Pre-commit hooks green (`prek run --all-files`).
- [ ] `CHANGELOG.md` updated under `## [Unreleased]` (if user-visible).
- [ ] Docs / `README.md` updated if behaviour or setup changed.
- [ ] Database migration added if entity schema changed.
