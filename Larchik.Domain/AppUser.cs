using Microsoft.AspNetCore.Identity;

namespace Larchik.Domain;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; }
}