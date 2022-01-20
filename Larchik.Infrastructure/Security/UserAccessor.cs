using System.Security.Claims;
using Larchik.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Larchik.Infrastructure.Security;

public class UserAccessor : IUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUsername() => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
}