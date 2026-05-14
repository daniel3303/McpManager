using AwesomeAssertions;
using McpManager.Web.Portal.Services;
using Xunit;

namespace McpManager.IntegrationTests.Services;

public class TokenCounterServiceTests
{
    [Fact]
    public void CountTokens_OnTypicalString_ReturnsPositiveCountAndZeroForEmpty()
    {
        // Constructing the service calls TiktokenTokenizer.CreateForModel("gpt-4o")
        // — a regression on the Microsoft.ML.Tokenizers version or the
        // "gpt-4o" model alias would throw here. A passing test proves both
        // the constructor path and the empty-string short-circuit are wired.
        var sut = new TokenCounterService();

        sut.CountTokens("").Should().Be(0);
        sut.CountTokens("Hello, world!").Should().BeGreaterThan(0);
    }
}
