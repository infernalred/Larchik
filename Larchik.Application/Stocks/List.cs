using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Stocks;

public class List
{
    public class Query : IRequest<OperationResult<List<StockDto>>>{}
    
    public class Handler : IRequestHandler<Query, OperationResult<List<StockDto>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        
        public Handler(ILogger<Handler> logger, DataContext context, IMapper mapper)
        {
            _logger = logger;
            _context = context;
            _mapper = mapper;
        }
        
        public async Task<OperationResult<List<StockDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var stock = await _context.Stocks
                .OrderBy(x => x.TypeId)
                .ProjectTo<StockDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            
            return OperationResult<List<StockDto>>.Success(stock);
        }
    }
}