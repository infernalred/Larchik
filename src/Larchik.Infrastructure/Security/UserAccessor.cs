using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Larchik.Application.Contracts;

namespace Larchik.Infrastructure.Security;

public class UserAccessor(IHttpContextAccessor httpContextAccessor) : IUserAccessor
{
    public Guid GetUserId()
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing.");
        }

        return Guid.Parse(userId);
    }
}
