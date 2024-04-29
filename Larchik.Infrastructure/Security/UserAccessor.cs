using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Larchik.Application.Contracts;

namespace Larchik.Infrastructure.Security;

public class UserAccessor(IHttpContextAccessor httpContextAccessor) : IUserAccessor
{
    public Guid GetUserId() =>
        Guid.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}