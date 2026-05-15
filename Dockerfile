FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Install Node.js 22 + npm via NodeSource for local MCP servers (npx).
# The Debian apt default is Node 18, which is too old for Vite 8 / Tailwind 4
# in the build stage; using NodeSource keeps both stages on the same major.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

# Data volume for SQLite + logs
RUN mkdir -p /app/data
VOLUME /app/data

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Node.js 22 for the frontend build (Vite 8 / Tailwind 4 require >=20).
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*

COPY . /src/
WORKDIR /src/src/McpManager.Web.Portal
RUN npm ci && npm run build
RUN dotnet restore && dotnet build -c Release -o /app/build

FROM build AS publish
WORKDIR /src/src/McpManager.Web.Portal
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "McpManager.Web.Portal.dll"]
