using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using McpManager.Web.Portal.Services.FlashMessage;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class FlashMessageClearTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public FlashMessageClearTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public void Clear_AfterQueuingMessage_EmptiesTempDataSoPeekReturnsNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var sut = new FlashMessage(
            sp.GetRequiredService<ITempDataDictionaryFactory>(),
            accessor,
            sp.GetRequiredService<IFlashMessageSerializer>()
        );

        sut.Success("will be wiped");
        sut.Peek().Should().ContainSingle("a queued banner is visible before Clear");

        // No production code calls IFlashMessage.Clear, so Clear() was
        // zero-hit. Pins that it empties TempData entirely — a regression
        // no-op'ing Clear (or clearing the wrong key) would leak banners into
        // the next request.
        sut.Clear();

        sut.Peek().Should().BeEmpty();
    }
}
