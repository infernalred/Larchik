// using Larchik.Application.Contracts;
// using Larchik.Application.Helpers;
// using Larchik.Application.Services.Contracts;
// using MediatR;
// using Microsoft.Extensions.Caching.Memory;
// using Microsoft.Extensions.Logging;
//
// namespace Larchik.Application.Portfolios;
//
// public class Details
// {
//     public class Query : IRequest<Result<Portfolio>> { }
//
//     public class Handler : IRequestHandler<Query, Result<Portfolio>>
//     {
//         private readonly ILogger<Handler> _logger;
//         private readonly IPortfolioService _portfolioService;
//         private readonly IMemoryCache _cache;
//         private readonly IUserAccessor _userAccessor;
//
//         public Handler(ILogger<Handler> logger, IPortfolioService portfolioService, IMemoryCache cache, IUserAccessor userAccessor)
//         {
//             _logger = logger;
//             _portfolioService = portfolioService;
//             _cache = cache;
//             _userAccessor = userAccessor;
//         }
//         
//         public async Task<Result<Portfolio>> Handle(Query request, CancellationToken cancellationToken)
//         {
//             var key = _userAccessor.GetUsername();
//             
//             if (!_cache.TryGetValue(key, out Portfolio portfolio))
//             {
//                 portfolio = await _portfolioService.GetPortfolioAsync(cancellationToken);
//                 
//                 _cache.Set(key, portfolio, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(90)));
//             }
//             
//             return Result<Portfolio>.Success(portfolio);
//         }
//     }
// }