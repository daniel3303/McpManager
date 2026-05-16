using AwesomeAssertions;
using McpManager.Web.Portal.Services;
using Xunit;

namespace McpManager.IntegrationTests;

public class TokenCounterServiceCountToolTokensTests
{
    [Fact]
    public void CountToolTokens_CombinesDescriptionAndSchema_IntoLargerCount()
    {
        var sut = new TokenCounterService();

        // CountToolTokens (description + " " + schema) was zero-hit — only the
        // single-string CountTokens path was tested. The tool-cost badge relies
        // on the schema being folded in; a regression dropping inputSchema from
        // the combined text would under-report large-schema tools.
        var descOnly = sut.CountTokens("a fairly short tool description");
        var combined = sut.CountToolTokens(
            "a fairly short tool description",
            "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}}}"
        );

        combined.Should().BeGreaterThan(descOnly, "the input schema must add tokens");
    }
}
