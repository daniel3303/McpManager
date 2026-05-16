using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpManager.IntegrationTests;

public class HttpContextExtensionsIsAjaxRequestTests
{
    [Fact]
    public void IsAjaxRequest_WithXmlHttpRequestHeader_ReturnsTrue()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        // IsAjaxRequest was entirely zero-hit. The header check is how the app
        // distinguishes fetch/XHR calls from full navigations (to return JSON
        // vs a redirect). A regression to the header name or comparison would
        // make AJAX endpoints respond with full-page redirects.
        ctx.Request.IsAjaxRequest().Should().BeTrue();
    }
}
