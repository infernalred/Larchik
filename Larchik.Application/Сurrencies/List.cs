using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using Larchik.Persistence.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Сurrencies;

public class List
{
    public class Query : IRequest<Result<List<Currency>>>{}
    
    public class Handler : IRequestHandler<Query, Result<List<Currency>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;

        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<Result<List<Currency>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var currency = await _context.Currencies
                .ToListAsync(cancellationToken);

            return Result<List<Currency>>.Success(currency);
        }
    }
}