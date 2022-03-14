using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Operations;

public class List
{
    public class Query : IRequest<OperationResult<List<Operation>>>{}

    public class Handler : IRequestHandler<Query, OperationResult<List<Operation>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        
        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<OperationResult<List<Operation>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var operation = await _context.Operations.ToListAsync(cancellationToken);
            
            return OperationResult<List<Operation>>.Success(operation);
        }
    }
}