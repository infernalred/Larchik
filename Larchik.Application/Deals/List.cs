using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Deals;

public class List
{
    public class Query : IRequest<OperationResult<List<DealDto>>>
    {
        public Guid Id { get; set; }
    }
    
    public class Handler : IRequestHandler<Query, OperationResult<List<DealDto>>>
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
        
        public async Task<OperationResult<List<DealDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var deals = await _context.Deals
                .AsNoTracking()
                .Where(x => x.AccountId == request.Id)
                .ProjectTo<DealDto>(_mapper.ConfigurationProvider)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
            
            return OperationResult<List<DealDto>>.Success(deals);
        }
    }
}