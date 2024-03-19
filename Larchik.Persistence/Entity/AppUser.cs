using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence.Entity;

public class AppUser : IdentityUser//<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}