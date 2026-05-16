using AwesomeAssertions;
using McpManager.Core.Data.Models.Mcp;
using McpManager.Core.Mcp;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.IntegrationTests;

public class McpNamespaceManagerValidateSlugNewlineTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public McpNamespaceManagerValidateSlugNewlineTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact(Skip = "GH-334 — slug regex $ anchor accepts a trailing newline")]
    public async Task Create_SlugWithTrailingNewline_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var nsManager = scope.ServiceProvider.GetRequiredService<McpNamespaceManager>();

        var ns = new McpNamespace
        {
            Name = $"ns-{Guid.NewGuid():N}",
            Slug = $"a{Guid.NewGuid():N}\n",
        };

        // Contract: the validation message is the spec — "Slug must contain
        // only lowercase letters, numbers, and hyphens". A newline is none of
        // those, so a slug ending in \n must be rejected. .NET regex `$` also
        // matches before a terminating \n, so `^[a-z0-9][a-z0-9-]*$` wrongly
        // accepts it (anchor should be \z) — slugs route URLs, \n enables drift.
        var act = async () => await nsManager.Create(ns);

        (await act.Should().ThrowAsync<ApplicationException>()).Which.Property.Should().Be("Slug");
    }
}
