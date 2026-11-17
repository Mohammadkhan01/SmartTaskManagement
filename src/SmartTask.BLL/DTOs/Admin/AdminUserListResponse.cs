namespace SmartTask.BLL.DTOs.Admin;

public sealed class AdminUserListResponse
{
    public IReadOnlyList<AdminUserResponse> Items { get; set; } =
        Array.Empty<AdminUserResponse>();

    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
