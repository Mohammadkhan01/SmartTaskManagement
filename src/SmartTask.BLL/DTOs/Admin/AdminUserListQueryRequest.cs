namespace SmartTask.BLL.DTOs.Admin;

public sealed class AdminUserListQueryRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
    public string SortBy { get; set; } = "email";
    public bool SortDescending { get; set; } = true;
}
