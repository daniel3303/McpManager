using AwesomeAssertions;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace McpManager.IntegrationTests.TagHelpers;

public class HeroIconTagHelperUnknownNameTests
{
    [Fact]
    public void Process_UnknownIconName_SuppressesOutput()
    {
        var sut = new HeroIconTagHelper { Name = "__definitely_not_an_icon__" };
        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString()
        );
        var output = new TagHelperOutput(
            "icon",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())
        );

        // HeroIcons.Render returns "" for an unknown name, so the empty-svg
        // guard must SuppressOutput — previously zero-hit (every view uses a
        // valid icon). A regression dropping the guard would emit an empty
        // <icon> element / malformed SVG instead of nothing.
        sut.Process(context, output);

        // SuppressOutput nulls the tag name and emits no content; the success
        // path instead leaves an SVG in Content, so these two together
        // distinguish suppression from a rendered icon.
        output.TagName.Should().BeNull();
        output.Content.GetContent().Should().BeEmpty();
    }
}
