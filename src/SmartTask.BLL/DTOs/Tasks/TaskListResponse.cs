namespace SmartTask.BLL.DTOs.Tasks;

public sealed class TaskListResponse
{
    public IReadOnlyList<TaskResponse> Items { get; set; } =
        Array.Empty<TaskResponse>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }
}
