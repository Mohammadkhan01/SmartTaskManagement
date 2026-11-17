using SmartTask.Common.Responses;
using SmartTask.Common.Utilities;

namespace SmartTask.UnitTests;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(99, 25, 4)]
    [InlineData(100, 25, 4)]
    [InlineData(5, 1, 5)]
    public void CalculateTotalPages_ShouldRoundUp(
        int totalCount,
        int pageSize,
        int expectedPages)
    {
        Assert.Equal(
            expectedPages,
            Pagination.CalculateTotalPages(totalCount, pageSize));
    }

    [Fact]
    public void CalculateTotalPages_ShouldNotOverflowNearIntMaxValue()
    {
        Assert.Equal(
            int.MaxValue,
            Pagination.CalculateTotalPages(int.MaxValue, 1));

        Assert.Equal(
            1,
            Pagination.CalculateTotalPages(int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void CalculateTotalPages_ShouldRejectNegativeTotalCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pagination.CalculateTotalPages(-1, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CalculateTotalPages_ShouldRejectNonPositivePageSize(int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pagination.CalculateTotalPages(10, pageSize));
    }

    [Theory]
    [InlineData(1, 0, false, false)]
    [InlineData(1, 1, false, false)]
    [InlineData(1, 3, false, true)]
    [InlineData(2, 3, true, true)]
    [InlineData(3, 3, true, false)]
    public void PagedResponse_ShouldDeriveNavigationFlags(
        int pageNumber,
        int totalPages,
        bool expectedHasPrevious,
        bool expectedHasNext)
    {
        var response = new PagedResponse<int>
        {
            PageNumber = pageNumber,
            TotalPages = totalPages
        };

        Assert.Equal(expectedHasPrevious, response.HasPreviousPage);
        Assert.Equal(expectedHasNext, response.HasNextPage);
    }
}
