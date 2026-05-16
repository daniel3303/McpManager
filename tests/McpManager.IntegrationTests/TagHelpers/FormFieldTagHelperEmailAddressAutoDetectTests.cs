using AngleSharp;
using AwesomeAssertions;
using McpManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace McpManager.IntegrationTests.TagHelpers;

public class FormFieldTagHelperEmailAddressAutoDetectTests : IClassFixture<WebFactoryFixture>
{
    private readonly WebFactoryFixture _factory;

    public FormFieldTagHelperEmailAddressAutoDetectTests(WebFactoryFixture factory) =>
        _factory = factory;

    [Fact]
    public async Task Process_PropertyWithEmailAddressAttribute_RendersInputTypeEmail()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            }
        );
        var ct = TestContext.Current.CancellationToken;
        await _factory.SignInAsAdminAsync(client, ct);

        var resp = await client.GetAsync("/Users/Create", ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);
        var doc = await BrowsingContext
            .New(Configuration.Default)
            .OpenAsync(req => req.Content(html), ct);

        // UserForCreateDto.Email has [EmailAddress] but NO [DataType]. The covered
        // path is the default text branch; this exercises the validator-metadata
        // loop where `validator is EmailAddressAttribute` returns "email". A
        // regression dropping that branch would silently downgrade the field to
        // type="text", losing client-side email validation.
        var email = doc.QuerySelector("input[name='Email']");
        email.Should().NotBeNull();
        email!.GetAttribute("type").Should().Be("email");
    }
}
