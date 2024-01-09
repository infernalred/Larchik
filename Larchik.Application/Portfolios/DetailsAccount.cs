using Larchik.Application.Helpers;
using Larchik.Application.Services.Contracts;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Portfolios;

public class DetailsAccount
{
    public class Query : IRequest<Result<Portfolio>>
    {
        public Guid Id { get; set; }
    }
    
    public class Handler : IRequestHandler<Query, Result<Portfolio>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly IPortfolioService _portfolioService;
        private readonly IMemoryCache _cache;

        public Handler(ILogger<Handler> logger, IPortfolioService portfolioService, IMemoryCache cache)
        {
            _logger = logger;
            _portfolioService = portfolioService;
            _cache = cache;
        }
        
        public async Task<Result<Portfolio>> Handle(Query request, CancellationToken cancellationToken)
        {
            if (!_cache.TryGetValue(request.Id, out Portfolio portfolio))
            {
                portfolio = await _portfolioService.GetPortfolioAsync(request.Id, cancellationToken);
                
                _cache.Set(request.Id, portfolio, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(90)));
            }
            
            return Result<Portfolio>.Success(portfolio);
        }
    }
}