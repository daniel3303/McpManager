using AwesomeAssertions;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.UnitTests.Core.Exceptions;

public class ApplicationExceptionErrorListCtorTests
{
    [Fact]
    public void Constructor_ErrorListOnly_JoinsMessageKeepsErrorsAndNullProperty()
    {
        var errors = new List<string> { "Name is required", "Slug is taken" };

        var ex = new ApplicationException(errors);

        // The list-only ctor (no property) was zero-hit — production builds
        // ApplicationException via the (message[,property]) overloads. A
        // regression that stopped joining errors into base.Message, or that
        // defaulted Property to "" instead of null, would change how callers
        // surface a property-less multi-error failure.
        ex.Message.Should().Be("Name is required; Slug is taken");
        ex.Errors.Should().Equal("Name is required", "Slug is taken");
        ex.Property.Should().BeNull();
    }
}
