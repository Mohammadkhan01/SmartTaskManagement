namespace SmartTask.BLL.DTOs.Projects;

public sealed class ProjectListResponse
{
    public IReadOnlyList<ProjectResponse> Items { get; set; }
        = Array.Empty<ProjectResponse>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
