using AwesomeAssertions;
using McpManager.Core.Data.Models.Authentication;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.IntegrationTests.Mcp;

public class McpServerManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpServerManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_WithEmptyName_ThrowsApplicationExceptionForName()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/",
                }
            );

        // ValidateServer's first guard. A regression that moves the name check
        // after persistence would let blank-named servers reach the DB and
        // break the McpServers list view.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Name");
    }

    [Fact]
    public async Task Create_WithHttpTransportAndNonAbsoluteUri_ThrowsApplicationExceptionForUri()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "bad-uri",
                    TransportType = McpTransportType.Http,
                    Uri = "not-a-real-uri",
                }
            );

        // ValidateServer's Uri.TryCreate guard. The MCP HTTP client gets the
        // raw string into a new Uri() — a non-absolute value crashes the
        // factory later if this guard ever moves.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Uri");
    }

    [Fact]
    public async Task Create_WithValidHttpServer_PersistsRowAndAssignsId()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var ct = TestContext.Current.CancellationToken;
        var name = $"integration-{Guid.NewGuid():N}";

        var created = await sut.Create(
            new McpServer
            {
                Name = name,
                TransportType = McpTransportType.Http,
                Uri = "https://example.invalid/mcp",
            }
        );

        // Round-trip via the repository proves SaveChanges fired and the entity
        // is queryable by Id — without that contract Update and Delete would
        // also be broken.
        created.Id.Should().NotBeEmpty();
        var persisted = await repo.GetAll().FirstOrDefaultAsync(s => s.Id == created.Id, ct);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(name);
    }

    [Fact]
    public async Task Update_AfterCreate_ChangesAreVisibleViaRepository()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var repo = scope.ServiceProvider.GetRequiredService<McpServerRepository>();
        var ct = TestContext.Current.CancellationToken;

        var server = await sut.Create(
            new McpServer
            {
                Name = $"to-update-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Http,
                Uri = "https://example.invalid/mcp",
            }
        );

        server.Description = "updated";
        await sut.Update(server);

        // Update calls SaveChanges on the tracked entity; if Validate ever
        // skipped on Update or SaveChanges wasn't awaited, the new
        // Description would not be observable via a fresh repository read.
        var persisted = await repo.GetAll().FirstAsync(s => s.Id == server.Id, ct);
        persisted.Description.Should().Be("updated");
    }

    [Fact]
    public async Task CheckHealth_WithUnreachableStdioCommand_ReturnsFalseAndStampsLastError()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpServerManager>();

        // /usr/bin/false exists on every Unix runner and exits non-zero
        // immediately. The stdio MCP client transport will fail to negotiate
        // and CheckHealth must catch, stamp LastError, and return false —
        // the only safe failure path for the operator UI.
        var server = await sut.Create(
            new McpServer
            {
                Name = $"unreachable-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "/usr/bin/false",
            }
        );

        var healthy = await sut.CheckHealth(server);

        healthy.Should().BeFalse();
        server.LastError.Should().NotBeNullOrWhiteSpace();
        server.LastError.Should().Contain("Failed to connect");
    }

    [Fact]
    public async Task Create_HttpWithBearerAuthAndNoToken_ThrowsApplicationExceptionForAuthToken()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "bearer-no-token",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/mcp",
                    Auth = new Auth { Type = AuthType.Bearer, Token = "" },
                }
            );

        // ValidateServer's Auth switch (case Bearer) was uncovered: an HTTP
        // server configured for Bearer auth must reject a blank token before
        // persistence. A regression collapsing the auth switch would let a
        // tokenless Bearer server through and 401 every proxied request.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Auth.Token");
    }

    [Fact]
    public async Task ApplyToolCustomizations_WithChangedDescription_PersistsCustomDescription()
    {
        Guid serverId;
        Guid toolId;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<McpServerManager>();
            var server = await manager.Create(
                new McpServer
                {
                    Name = $"apply-{Guid.NewGuid():N}",
                    TransportType = McpTransportType.Http,
                    Uri = "https://upstream.invalid/mcp",
                }
            );
            serverId = server.Id;
            var tools = scope.ServiceProvider.GetRequiredService<McpToolRepository>();
            var tool = tools.Add(
                new McpTool
                {
                    Name = "do_thing",
                    Description = "original desc",
                    McpServerId = server.Id,
                    InputSchema = "{}",
                }
            );
            await tools.SaveChanges();
            toolId = tool.Id;

            await manager.ApplyToolCustomizations(
                server,
                [("do_thing", "original desc", "a better description")]
            );
        }

        // ApplyToolCustomizations' change path was uncovered: skip-empty/unchanged
        // guard is bypassed, the tool is resolved by name, CustomDescription is
        // trimmed-assigned and SaveChanges fires only when hasChanges. Asserting
        // the persisted value pins the whole write path (a regression that drops
        // the hasChanges flag would silently not save).
        using var verify = _factory.Services.CreateScope();
        var toolRepo = verify.ServiceProvider.GetRequiredService<McpToolRepository>();
        var reloaded = await toolRepo.Get(toolId);
        reloaded!.CustomDescription.Should().Be("a better description");
        reloaded.McpServerId.Should().Be(serverId);
    }

    [Fact]
    public async Task CheckHealth_OpenApiUnreachable_SetsLastErrorAndReturnsFalse()
    {
        var sut = ResolveServerManager();
        // CheckHealth writes a server log + notifications keyed on the server,
        // so it must be persisted first (a detached entity FK-fails on save).
        var server = await sut.Create(
            new McpServer
            {
                Name = $"openapi-health-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.invalid.invalid/",
                OpenApiSpecification = "{}",
            }
        );

        // The OpenApi branch of CheckHealth (lines 111-113: _openApiExecutor
        // .CheckHealth -> throw on !healthy) was uncovered; only the Http/Stdio
        // path was. An unreachable OpenApi base URL must flow into the catch:
        // LastError set + false returned (a regression skipping the OpenApi
        // branch would wrongly report the server healthy).
        var healthy = await sut.CheckHealth(server);

        healthy.Should().BeFalse();
        server.LastError.Should().NotBeNullOrEmpty("CheckHealth must record the failure reason");
    }

    [Fact]
    public async Task SyncTools_OpenApiServer_ParsesSpecAndAddsTools()
    {
        var sut = ResolveServerManager();
        var server = await sut.Create(
            new McpServer
            {
                Name = $"openapi-sync-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.example.invalid/",
                OpenApiSpecification = """
                openapi: 3.0.0
                info:
                  title: Test API
                  version: "1"
                paths:
                  /things:
                    get:
                      operationId: listThings
                      summary: List things
                      responses:
                        '200':
                          description: OK
                """,
            }
        );

        // SyncTools' OpenApi branch (lines 154-163) was uncovered: it parses
        // the spec locally (no upstream) into ToolSyncEntry -> MergeTools. The
        // Http/Stdio path needs a live server and only its catch was hit. A
        // regression dropping the OpenApi branch syncs zero tools for every
        // OpenAPI server while still reporting success.
        var result = await sut.SyncTools(server);

        result.Success.Should().BeTrue();
        result.ToolsAdded.Should().BeGreaterThan(0, "the spec defines one operation");
    }

    [Fact]
    public async Task SyncTools_OpenApiResyncWithChangedSpec_UpdatesAndRemovesTools()
    {
        var sut = ResolveServerManager();
        const string specWithBoth = """
            openapi: 3.0.0
            info:
              title: T
              version: "1"
            paths:
              /alpha:
                get:
                  operationId: alpha
                  summary: A first
                  responses:
                    '200':
                      description: OK
              /beta:
                get:
                  operationId: beta
                  summary: B
                  responses:
                    '200':
                      description: OK
            """;
        const string specAlphaChangedOnly = """
            openapi: 3.0.0
            info:
              title: T
              version: "1"
            paths:
              /alpha:
                get:
                  operationId: alpha
                  summary: A changed
                  responses:
                    '200':
                      description: OK
            """;

        var server = await sut.Create(
            new McpServer
            {
                Name = $"resync-{Guid.NewGuid():N}",
                TransportType = McpTransportType.OpenApi,
                Uri = "https://api.example.invalid/",
                OpenApiSpecification = specWithBoth,
            }
        );
        await sut.SyncTools(server); // adds alpha + beta

        server.OpenApiSpecification = specAlphaChangedOnly;
        await sut.Update(server);

        // Re-sync: MergeTools' update path (alpha summary changed -> ToolsUpdated)
        // and remove path (beta gone from spec -> ToolsRemoved) were uncovered;
        // only the add path was. A regression in the diff would duplicate alpha
        // or orphan beta on every re-sync.
        var result = await sut.SyncTools(server);

        result.Success.Should().BeTrue();
        result.ToolsUpdated.Should().BeGreaterThan(0, "alpha's description changed");
        result.ToolsRemoved.Should().BeGreaterThan(0, "beta was dropped from the spec");
    }

    [Fact]
    public async Task Create_HttpWithApiKeyAuthAndNoKeyName_ThrowsApplicationExceptionForApiKeyName()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "apikey-no-name",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/mcp",
                    Auth = new Auth
                    {
                        Type = AuthType.ApiKey,
                        ApiKeyName = "",
                        ApiKeyValue = "v",
                    },
                }
            );

        // ValidateServer's Auth switch (case ApiKey, first guard) was
        // uncovered: an HTTP server using ApiKey auth must reject a blank key
        // name before persistence. A regression collapsing it would let a
        // nameless ApiKey server through and send credential-less requests.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Auth.ApiKeyName");
    }

    [Fact]
    public async Task Create_HttpWithApiKeyAuthNameButNoValue_ThrowsApplicationExceptionForApiKeyValue()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "apikey-no-value",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/mcp",
                    Auth = new Auth
                    {
                        Type = AuthType.ApiKey,
                        ApiKeyName = "X-Api-Key",
                        ApiKeyValue = "",
                    },
                }
            );

        // ValidateServer's ApiKey second guard (name present, value blank) was
        // uncovered (#124 pinned the name guard). A regression collapsing it
        // would persist a server that sends an empty API-key header and silently
        // 401s every proxied request.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Auth.ApiKeyValue");
    }

    [Fact]
    public async Task Create_HttpWithBasicAuthAndNoUsername_ThrowsApplicationExceptionForAuthUsername()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "basic-no-username",
                    TransportType = McpTransportType.Http,
                    Uri = "https://example.invalid/mcp",
                    Auth = new Auth { Type = AuthType.Basic, Username = "" },
                }
            );

        // ValidateServer's Auth switch (case Basic) was uncovered (#98 pinned
        // Bearer, #124/#128 ApiKey). A Basic-auth HTTP server must reject a
        // blank username before persistence — a collapsed guard would send
        // malformed Basic credentials and 401 every proxied request.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Auth.Username");
    }

    [Fact]
    public async Task Create_StdioWithBlankCommand_ThrowsApplicationExceptionForCommand()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "stdio-no-command",
                    TransportType = McpTransportType.Stdio,
                    Command = "",
                }
            );

        // ValidateServer's Stdio else-branch guard (blank Command) was the last
        // uncovered transport guard. A regression collapsing it would persist a
        // Stdio server with no command — the client factory then fails to spawn
        // a process and every tool call on that server errors.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Command");
    }

    [Fact]
    public async Task Create_OpenApiWithNonHttpUri_ThrowsApplicationExceptionForUri()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "openapi-ftp",
                    TransportType = McpTransportType.OpenApi,
                    Uri = "ftp://example.com/spec",
                    OpenApiSpecification = "{}",
                }
            );

        // ValidateServer's OpenAPI scheme guard (Uri.TryCreate + http/https
        // check, line 550) was uncovered — prior OpenAPI tests used valid URLs.
        // A non-HTTP scheme must be rejected before the spec check; collapsing
        // this guard would let OpenApiToolExecutor build an unusable HttpClient.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Uri");
    }

    [Fact]
    public async Task Create_OpenApiWithBlankUri_ThrowsApplicationExceptionForUri()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "openapi-no-uri",
                    TransportType = McpTransportType.OpenApi,
                    Uri = "",
                    OpenApiSpecification = "{}",
                }
            );

        // ValidateServer's OpenAPI blank-Uri guard (line 539) was uncovered —
        // prior OpenAPI tests always supplied a Uri. It must fire before the
        // scheme check; collapsing it would persist an OpenAPI server with no
        // base URL, so every tool call builds a request against an empty host.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Uri");
    }

    [Fact]
    public async Task Create_SseWithBlankUri_ThrowsApplicationExceptionForUri()
    {
        var sut = ResolveServerManager();

        var act = async () =>
            await sut.Create(
                new McpServer
                {
                    Name = "sse-no-uri",
                    TransportType = McpTransportType.Sse,
                    Uri = "",
                }
            );

        // ValidateServer's Http/Sse blank-Uri guard (line 565) was uncovered —
        // the existing HTTP test used a non-blank invalid URI (hits the later
        // TryCreate guard, not this one). Collapsing it would persist an SSE
        // server with no URI, crashing the client factory on first connect.
        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Uri");
    }

    private McpServerManager ResolveServerManager()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<McpServerManager>();
    }
}
