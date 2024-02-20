// using System.Security.Claims;
// using Larchik.Persistence.Context;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Http;
// using Microsoft.EntityFrameworkCore;
//
// namespace Larchik.Infrastructure.Security;
//
// public class IsDealOwnerRequirement : IAuthorizationRequirement { }
//
// public class IsDealOwnerRequirementRequirementHandler : AuthorizationHandler<IsDealOwnerRequirement>
// {
//     private readonly DataContext _dbContext;
//     private readonly IHttpContextAccessor _httpContextAccessor;
//     
//     public IsDealOwnerRequirementRequirementHandler(DataContext dbContext, IHttpContextAccessor httpContextAccessor)
//     {
//         _dbContext = dbContext;
//         _httpContextAccessor = httpContextAccessor;
//     }
//     
//     protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, IsDealOwnerRequirement requirement)
//     {
//         var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
//         
//         if (userId == null) return Task.CompletedTask;
//
//         var dealId = Guid.Parse(_httpContextAccessor.HttpContext?.Request.RouteValues
//             .SingleOrDefault(x => x.Key == "id").Value?.ToString() ?? string.Empty);
//
//         var deal = _dbContext.Deals
//             .AsNoTracking()
//             .SingleOrDefaultAsync(x => x.UserId == Guid.Parse(userId) && x.Id == dealId).Result;
//         
//         if (deal != null) context.Succeed(requirement);
//         
//         return Task.CompletedTask;
//     }
// }