using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Reports;

public class CurrencyOperationsReport
{
    public class Query : IRequest<OperationResult<CurrencyOperationsReportModel>>
    {
        public Guid AccountId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class Handler : IRequestHandler<Query, OperationResult<CurrencyOperationsReportModel>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;

        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<OperationResult<CurrencyOperationsReportModel>> Handle(Query request, CancellationToken cancellationToken)
        {
            var deals = await _context.Deals
                .Include(x => x.Stock)
                .Where(x => x.AccountId == request.AccountId && x.CreatedAt >= request.FromDate.ToUniversalTime() && x.CreatedAt <= request.ToDate.ToUniversalTime() && x.Stock.TypeId == "MONEY")
                .GroupBy(x => new { x.StockId, x.OperationId }, (key, group) => new CurrencyDealsReport
                {
                    Currency = key.StockId,
                    Operation = key.OperationId,
                    Amount = group.Sum(x => x.Amount)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            
            var result = new CurrencyOperationsReportModel
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Operations = deals
            };

            return OperationResult<CurrencyOperationsReportModel>.Success(result);
        }
    }
}