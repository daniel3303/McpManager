using AwesomeAssertions;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.UnitTests.Core.Exceptions;

public class ApplicationExceptionMessageCtorTests
{
    [Fact]
    public void Constructor_MessageOnly_SetsMessageSeedsErrorsAndNullProperty()
    {
        var ex = new ApplicationException("Something went wrong");

        // The message-only ctor was zero-hit — production builds this type via
        // the (message, property) / (errors) overloads. Callers iterate
        // .Errors for per-field ModelState entries, so a regression that left
        // Errors empty here would silently drop the single-message failure.
        ex.Message.Should().Be("Something went wrong");
        ex.Errors.Should().Equal("Something went wrong");
        ex.Property.Should().BeNull();
    }
}
