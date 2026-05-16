using System.Net;
using AwesomeAssertions;
using McpManager.Web.Portal.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpManager.IntegrationTests;

public class HttpContextExtensionsRemoteIpFallbackTests
{
    [Fact]
    public void GetRemoteIpAddress_NoCloudflareHeader_FallsBackToConnectionRemoteIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        // With no CF-Connecting-IP header the method must fall back to
        // Connection.RemoteIpAddress (lines 20-22) and parse it. That branch
        // was zero-hit (tests/requests always set the Cloudflare header). A
        // regression skipping the fallback would null the IP used for audit
        // logging and rate limiting.
        var ip = ctx.GetRemoteIpAddress();

        ip.Should().Be(IPAddress.Parse("203.0.113.7"));
    }
}
