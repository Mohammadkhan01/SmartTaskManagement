namespace SmartTask.Common.Utilities;

public static class Pagination
{
    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        return (int)(((long)totalCount + pageSize - 1) / pageSize);
    }
}
