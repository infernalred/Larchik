using AutoMapper;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Deals;

public class Details
{
    public class Query : IRequest<OperationResult<DealDto>>
    {
        public Guid Id { get; set; }
    }
    
    public class Handler : IRequestHandler<Query, OperationResult<DealDto>>
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
        
        public async Task<OperationResult<DealDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var deal = await _context.Deals
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            
            return OperationResult<DealDto>.Success(_mapper.Map<DealDto>(deal));
        }
    }
}