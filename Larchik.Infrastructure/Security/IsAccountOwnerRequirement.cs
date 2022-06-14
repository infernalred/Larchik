using System.Security.Claims;
using Larchik.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Infrastructure.Security;

public class IsAccountOwnerRequirement : IAuthorizationRequirement { }

public class IsAccountOwnerRequirementHandler : AuthorizationHandler<IsAccountOwnerRequirement>
{
    private readonly DataContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public IsAccountOwnerRequirementHandler(DataContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }
    
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IsAccountOwnerRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (userId == null) return Task.CompletedTask;

        var accountId = Guid.Parse(_httpContextAccessor.HttpContext?.Request.RouteValues
            .SingleOrDefault(x => x.Key == "id").Value?.ToString() ?? string.Empty);

        var account = _dbContext.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == accountId).Result;
        
        if (account != null) context.Succeed(requirement);
        
        return Task.CompletedTask;
    }
}