using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence.Models;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}