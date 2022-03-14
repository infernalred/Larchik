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
            var account = await _context.Assets.Where(x => x.AccountId == request.AccountId)
            throw new NotImplementedException();
        }
    }
}