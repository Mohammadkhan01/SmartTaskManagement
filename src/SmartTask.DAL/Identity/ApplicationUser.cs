using Microsoft.AspNetCore.Identity;

namespace SmartTask.DAL.Identity;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;
}
