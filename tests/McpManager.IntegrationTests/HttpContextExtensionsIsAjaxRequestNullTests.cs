using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpManager.IntegrationTests;

public class HttpContextExtensionsIsAjaxRequestNullTests
{
    [Fact]
    public void IsAjaxRequest_NullRequest_ThrowsArgumentNullException()
    {
        // The `request == null` guard was zero-hit (every caller passes a real
        // request). Pins that a null request fails fast with a clear
        // ArgumentNullException rather than a bare NRE deep in the header
        // lookup — a regression dropping the guard would obscure the cause.
        var act = () => ((HttpRequest)null).IsAjaxRequest();

        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }
}
