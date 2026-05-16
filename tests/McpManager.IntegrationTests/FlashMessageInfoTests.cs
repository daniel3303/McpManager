using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using McpManager.Web.Portal.Services.FlashMessage;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpManager.IntegrationTests;

public class FlashMessageInfoTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public FlashMessageInfoTests(WebFactoryFixture factory) => _factory = factory;

    [Fact]
    public void Info_QueuesMessageWithInfoTypeAndRoundTripsThroughSerializer()
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

        // No production code calls IFlashMessage.Info, so the whole Info()
        // method (build FlashMessageModel + Queue + Store + serialize) was
        // zero-hit. Peek() round-trips it back through the serializer, pinning
        // that an Info banner keeps Type=Info and its message/title — a
        // regression copy-pasting Success's Type here would silently mislabel.
        sut.Info("Heads up", "Notice");

        var queued = sut.Peek();
        queued.Should().ContainSingle();
        queued[0].Type.Should().Be(FlashMessageType.Info);
        queued[0].Message.Should().Be("Heads up");
        queued[0].Title.Should().Be("Notice");
        queued[0].IsHtml.Should().BeFalse();
    }
}
