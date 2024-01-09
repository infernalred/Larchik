using System.Security.Claims;
using Larchik.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Larchik.Infrastructure.Security;

public class UserAccessor(IHttpContextAccessor httpContextAccessor) : IUserAccessor
{
    public string GetUsername() => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)!;
    public Guid GetUserId() => 
        Guid.Parse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}