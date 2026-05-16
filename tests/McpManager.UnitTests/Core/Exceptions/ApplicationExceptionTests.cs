using AwesomeAssertions;
using Xunit;
using ApplicationException = McpManager.Core.Data.Exceptions.ApplicationException;

namespace McpManager.UnitTests.Core.Exceptions;

public class ApplicationExceptionTests
{
    [Fact]
    public void Constructor_PropertyAndErrorList_JoinsMessageAndPreservesPropertyAndErrors()
    {
        var errors = new List<string> { "Name is required", "Slug is taken" };

        var ex = new ApplicationException("Slug", errors);

        // This ctor was zero-hit. Controllers surface ApplicationException by
        // reading .Property (the field key) and iterating .Errors for per-field
        // ModelState entries, while base.Message is the user-facing summary. A
        // regression that stopped joining (or dropped the list) would collapse
        // multi-error validation feedback to a single/blank message.
        ex.Property.Should().Be("Slug");
        ex.Errors.Should().Equal("Name is required", "Slug is taken");
        ex.Message.Should().Be("Name is required; Slug is taken");
    }
}
