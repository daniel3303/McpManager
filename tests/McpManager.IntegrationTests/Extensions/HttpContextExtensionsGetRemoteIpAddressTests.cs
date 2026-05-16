using System.Net;
using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpManager.IntegrationTests.Extensions;

public class HttpContextExtensionsGetRemoteIpAddressTests
{
    [Fact]
    public void GetRemoteIpAddress_WithCloudflareHeader_PrefersItOverConnectionRemoteIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        ctx.Request.Headers["CF-Connecting-IP"] = "203.0.113.7";

        // GetRemoteIpAddress was entirely zero-hit. Behind Cloudflare the real
        // client IP is in CF-Connecting-IP; Connection.RemoteIpAddress is the
        // edge proxy. A regression that read the connection IP first (or parsed
        // the wrong header) would mis-attribute every request's source IP in
        // logs and rate-limiting — this pins the CF-over-connection precedence
        // and the IPAddress.TryParse success path.
        var ip = ctx.GetRemoteIpAddress();

        ip.Should().Be(IPAddress.Parse("203.0.113.7"));
    }
}
