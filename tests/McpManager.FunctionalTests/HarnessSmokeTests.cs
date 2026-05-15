using System.Net;
using AwesomeAssertions;
using McpManager.FunctionalTests.Fixtures;
using Xunit;

namespace McpManager.FunctionalTests;

/// <summary>
/// Proves the load-bearing harness decision: the real app is reachable over a
/// genuine loopback socket (so a browser can hit it) while running in-process
/// (so the coverage collector instruments it). Shares the single e2e host —
/// the app entry point can only be hosted once per test process.
/// </summary>
[Collection("e2e")]
public class HarnessSmokeTests
{
    private readonly E2eFixture _e2e;

    public HarnessSmokeTests(E2eFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Host_ServesLoginOverRealSocket()
    {
        _e2e.BaseUrl.Should().StartWith("http://127.0.0.1:");

        using var http = new HttpClient { BaseAddress = new Uri(_e2e.BaseUrl) };
        var response = await http.GetAsync("/auth/login", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should()
            .Contain("loginForm");
    }
}
