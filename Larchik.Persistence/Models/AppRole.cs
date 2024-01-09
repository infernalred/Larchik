using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence.Models;

public class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}