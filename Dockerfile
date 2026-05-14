FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Install Node.js + npm in runtime for local MCP servers (npx)
RUN apt-get update && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

# Data volume for SQLite + logs
RUN mkdir -p /app/data
VOLUME /app/data

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Node.js for frontend build
RUN apt-get update && apt-get install -y nodejs npm

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
