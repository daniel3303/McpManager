# Security Policy

## Supported versions

Security fixes land in the latest released version. Older versions are not back-ported.

| Version  | Supported          |
|----------|--------------------|
| 1.0.x    | :white_check_mark: |
| < 1.0    | :x:                |

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities. Report them privately via GitHub's private vulnerability reporting form:

<https://github.com/daniel3303/McpManager/security/advisories/new>

What to include:

- A clear description of the issue and the affected component (Portal, MCP proxy endpoint, an upstream-server client, auth flow, etc.).
- A minimal reproduction or proof-of-concept.
- The McpManager version (Docker tag, commit SHA, or release tag).
- Your assessment of impact (auth bypass, RCE, info disclosure, denial of service, etc.).

## Response expectations

- **Acknowledgement** within 3 business days of receipt.
- **Triage and severity assignment** within 7 business days.
- **Fix and coordinated disclosure** scheduled with you once a patch is ready.

Please give us a reasonable window to fix the issue before any public disclosure. We're happy to credit you in the advisory and release notes if you'd like.
