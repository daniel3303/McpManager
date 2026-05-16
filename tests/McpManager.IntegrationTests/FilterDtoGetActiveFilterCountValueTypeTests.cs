using AwesomeAssertions;
using McpManager.Web.Portal.Dtos;
using Xunit;

namespace McpManager.IntegrationTests;

public class FilterDtoGetActiveFilterCountValueTypeTests
{
    private sealed class ValueTypeFilter : IFilterDto
    {
        public int Page { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void GetActiveFilterCount_NonDefaultValueTypeProperty_CountsIt()
    {
        // Production IFilterDto implementers only have string/nullable props,
        // so the IsValueType branch (Activator.CreateInstance default compare)
        // was zero-hit. A non-default int must count as an active filter; a
        // regression comparing against null instead of default(T) would make
        // value-type filters silently invisible in the "N filters" badge.
        IFilterDto filter = new ValueTypeFilter { Page = 3, Name = null };

        filter.GetActiveFilterCount().Should().Be(1);
    }
}
