using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.DealTypes;

public class List
{
    public class Query : IRequest<OperationResult<List<DealType>>>{}

    public class Handler : IRequestHandler<Query, OperationResult<List<DealType>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        
        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<OperationResult<List<DealType>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var dealTypes = await _context.DealTypes.ToListAsync(cancellationToken);
            
            return OperationResult<List<DealType>>.Success(dealTypes);
        }
    }
}