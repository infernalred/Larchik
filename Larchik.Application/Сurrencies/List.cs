using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Сurrencies;

public class List
{
    public class Query : IRequest<OperationResult<List<Currency>>>{}
    
    public class Handler : IRequestHandler<Query, OperationResult<List<Currency>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;

        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<OperationResult<List<Currency>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var currency = await _context.Currencies
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return OperationResult<List<Currency>>.Success(currency);
        }
    }
}