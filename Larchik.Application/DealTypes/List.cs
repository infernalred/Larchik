using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.DealTypes;

public class List
{
    public class Query : IRequest<Result<List<DealType>>>{}

    public class Handler : IRequestHandler<Query, Result<List<DealType>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        
        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<Result<List<DealType>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var dealTypes = await _context.DealTypes.ToListAsync(cancellationToken);
            
            return Result<List<DealType>>.Success(dealTypes);
        }
    }
}