# MCP Manager

**A self-hosted MCP proxy and aggregation platform.**

Manage multiple upstream MCP (Model Context Protocol) servers, sync their tools, and expose them through a single unified MCP endpoint. Connect once, access everything.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![MCP](https://img.shields.io/badge/MCP-v0.6-blue)
![SQLite](https://img.shields.io/badge/SQLite-Database-003B57?logo=sqlite)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)

---

## What is MCP Manager?

MCP Manager sits between your AI tools (Claude Code, Cursor, Windsurf, etc.) and your MCP servers. Instead of configuring each server individually in every client, you register them once in MCP Manager and connect your clients to a single endpoint.

```mermaid
graph LR
    A[Claude Code] --> P[MCP Manager<br/>proxy]
    B[Cursor] --> P
    C[Windsurf] --> P
    D[...] --> P
    P --> S1[MCP Server A]
    P --> S2[MCP Server B]
    P --> S3[MCP Server C]
    P --> S4[...]
```

## Key Features

- **Unified MCP Proxy** — Aggregate tools from multiple MCP servers into a single `/mcp` endpoint
- **Multi-Transport Support** — Connect to HTTP, Stdio, and OpenAPI-based servers
- **Namespace Organization** — Group tools into namespaces with independent rate limiting
- **Interactive Playground** — Test and execute MCP tools directly from the browser
- **API Key Management** — Scoped API keys with namespace-level access control
- **Import from Config** — Import servers from Cursor, Claude Desktop, or Opencode configurations
- **Request Logging** — Full audit trail of every MCP request
- **Live Log Streaming** — Real-time log viewer for debugging
- **User Management** — Multi-user support with role-based access
- **OpenAPI-to-MCP** — Automatically convert OpenAPI specs into MCP tools

## Screenshots

| | |
|---|---|
| ![Dashboard](docs/screenshots/dashboard.png) **Dashboard** — Overview of servers, tools, and API keys | ![MCP Servers](docs/screenshots/mcp-servers.png) **Servers** — Manage upstream MCP servers |
| ![Create Server](docs/screenshots/mcp-server-create.png) **Create Server** — Add servers with multi-transport support | ![Playground](docs/screenshots/mcp-playground.png) **Playground** — Execute tools interactively |
| ![Namespaces](docs/screenshots/mcp-namespaces.png) **Namespaces** — Organize tools with rate limiting | ![API Keys](docs/screenshots/api-keys.png) **API Keys** — Scoped key management |
| ![Request Log](docs/screenshots/mcp-requests.png) **Request Log** — Audit trail | ![Live Logs](docs/screenshots/live-logs.png) **Live Logs** — Real-time streaming |
| ![Import](docs/screenshots/mcp-server-import.png) **Import** — Import from Claude Desktop / Cursor | ![Users](docs/screenshots/users.png) **Users** — User management |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for frontend build)

### Run Locally

```bash
# Clone the repository
git clone https://github.com/your-org/McpManager.git
cd McpManager

# Build the frontend
cd McpManager.Web.Portal && npm install && npx vite build && cd ..

# Run the application
dotnet run --project McpManager.Web.Portal
```

The app will be available at `http://localhost:5057`. A default admin account is created on first run.

### Docker

```bash
docker build -t mcpmanager .
docker run -p 5057:8080 -v mcpmanager-data:/app/data mcpmanager
```

The SQLite database and logs are stored in `/app/data`.

## Connecting Your AI Tools

Once MCP Manager is running, connect your AI tools to the unified endpoint:

```jsonc
// Example: Claude Code, Cursor, Windsurf, etc.
{
  "mcpServers": {
    "mcpmanager": {
      "url": "http://localhost:5057/mcp",
      "headers": {
        "Authorization": "Bearer YOUR_API_KEY"
      }
    }
  }
}
```

Generate API keys from the **API Keys** page in the admin panel.

## Configuration

### Transport Types

| Transport | Description | Auth Options |
|-----------|-------------|--------------|
| **HTTP** | Connect to remote MCP servers via HTTP/SSE | Bearer token, API key, Basic auth |
| **Stdio** | Run local MCP servers as CLI processes | Environment variables |
| **OpenAPI** | Auto-convert OpenAPI specs to MCP tools | Bearer token, API key, Basic auth |

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | Listening URLs | `http://+:8080` |
| `ConnectionStrings__DefaultConnection` | SQLite connection string | `data/mcpmanager.db` |

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core, EF Core, SQLite
- **Frontend**: Tailwind CSS, DaisyUI, Vite, jQuery
- **MCP SDK**: ModelContextProtocol v0.6.0-preview.1
- **Auth**: ASP.NET Identity
- **Logging**: Serilog (console + rolling file)

## License

MIT
