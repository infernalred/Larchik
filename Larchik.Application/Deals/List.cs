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
    public class Query : IRequest<OperationResult<PagedList<DealDto>>>
    {
        public Guid Id { get; set; }
        public DealParams Params { get; set; } = null!;
    }
    
    public class Handler : IRequestHandler<Query, OperationResult<PagedList<DealDto>>>
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
        
        public async Task<OperationResult<PagedList<DealDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var query = _context.Deals
                .AsNoTracking()
                .Where(x => x.AccountId == request.Id)
                .OrderByDescending(x => x.CreatedAt)
                .ProjectTo<DealDto>(_mapper.ConfigurationProvider)
                .AsQueryable();

            if (request.Params.Ticker != null)
            {
                query = query.Where(x => x.Stock.Contains(request.Params.Ticker.ToUpper()) || x.Currency.Contains(request.Params.Ticker));
            }

            if (request.Params.Operation != null)
            {
                query = query.Where(x => x.Operation == request.Params.Operation);
            }
            
            return OperationResult<PagedList<DealDto>>.Success(
                await PagedList<DealDto>.CreateAsync(query, request.Params.PageNumber, request.Params.PageSize));
        }
    }
}