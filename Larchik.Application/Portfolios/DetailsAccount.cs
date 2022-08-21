using Larchik.Application.Helpers;
using Larchik.Application.Services.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Portfolios;

public class DetailsAccount
{
    public class Query : IRequest<OperationResult<Portfolio>>
    {
        public Guid Id { get; set; }
    }
    
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
            var portfolio = await _portfolioService.GetPortfolioAsync(request.Id, cancellationToken);
            
            return OperationResult<Portfolio>.Success(portfolio);
        }
    }
}