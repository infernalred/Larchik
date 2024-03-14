using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence.Entity;

public class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}