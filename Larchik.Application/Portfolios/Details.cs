using Larchik.Application.Helpers;
using Larchik.Application.Services.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Portfolios;

public class Details
{
    public class Query : IRequest<OperationResult<Portfolio>> { }

    public class Handler : IRequestHandler<Query, OperationResult<Portfolio>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly IPortfolioService _portfolioService;

        public Handler(ILogger<Handler> logger, IPortfolioService portfolioService)
        {
            _logger = logger;
            _portfolioService = portfolioService;
        }
        
        public async Task<OperationResult<Portfolio>> Handle(Query request, CancellationToken cancellationToken)
        {
            var portfolio = await _portfolioService.GetPortfolioAsync(cancellationToken);
            
            return OperationResult<Portfolio>.Success(portfolio);
        }
    }
}