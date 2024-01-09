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
    public class Query : IRequest<Result<DealDto>>
    {
        public Guid Id { get; set; }
    }
    
    public class Handler : IRequestHandler<Query, Result<DealDto>>
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
        
        public async Task<Result<DealDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var deal = await _context.Deals
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            
            return Result<DealDto>.Success(_mapper.Map<DealDto>(deal));
        }
    }
}