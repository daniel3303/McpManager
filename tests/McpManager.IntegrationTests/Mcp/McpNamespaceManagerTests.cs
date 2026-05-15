using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.Core.Repositories.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.IntegrationTests.Mcp;

public class McpNamespaceManagerTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceManagerTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public async Task Create_WithSlugContainingUppercase_ThrowsApplicationExceptionForSlug()
    {
        using var scope = _factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();

        // The slug regex (^[a-z0-9][a-z0-9-]*$) is the only guard preventing
        // operators from creating a namespace whose slug breaks the
        // /mcp/ns/{slug} route (which lowercases via LowercaseUrls = true and
        // would not match an uppercase slug). The McpNamespaceDto annotation
        // catches it in the form path; the manager catches it for any other
        // caller (e.g. import) — a regression in either would let a broken
        // namespace persist.
        var act = async () =>
            await sut.Create(
                new McpNamespace
                {
                    Name = "Bad",
                    Slug = "Has-UPPERCASE",
                    Description = "",
                }
            );

        var ex = await act.Should().ThrowAsync<ApplicationException>();
        ex.Which.Property.Should().Be("Slug");
    }

    [Fact]
    public async Task AddServer_WhenLinkAlreadyExists_ReturnsExistingWithoutDuplicating()
    {
        using var scope = _factory.Services.CreateScope();
        var nsMgr = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();
        var serverMgr = scope.ServiceProvider.GetRequiredService<McpServerManager>();
        var nsServerRepo = scope.ServiceProvider.GetRequiredService<McpNamespaceServerRepository>();
        var ct = TestContext.Current.CancellationToken;

        var slug = $"ns-{Guid.NewGuid():N}";
        var ns = await nsMgr.Create(
            new McpNamespace
            {
                Name = slug,
                Slug = slug,
                Description = "",
            }
        );
        var server = await serverMgr.Create(
            new McpServer
            {
                Name = $"srv-{Guid.NewGuid():N}",
                TransportType = McpTransportType.Stdio,
                Command = "dotnet",
                Arguments = [TestStdioServerLocator.DllPath],
            }
        );

        // AddServer's "link already exists" early-return (line 75) was
        // uncovered. Re-adding the same server to a namespace must be
        // idempotent — a regression would insert a duplicate
        // McpNamespaceServer row and double every aggregated tool.
        var first = await nsMgr.AddServer(ns, server);
        var second = await nsMgr.AddServer(ns, server);

        second.Id.Should().Be(first.Id);
        var links = await nsServerRepo
            .GetByNamespace(ns)
            .CountAsync(s => s.McpServerId == server.Id, ct);
        links.Should().Be(1);
    }
}
