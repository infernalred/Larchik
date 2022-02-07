using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Brokers;

public class List
{
    public class Query : IRequest<OperationResult<List<BrokerDto>>>{}
    
    public class Handler : IRequestHandler<Query, OperationResult<List<BrokerDto>>>
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
        
        public async Task<OperationResult<List<BrokerDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var brokers = await _context.Brokers
                .ProjectTo<BrokerDto>(_mapper.ConfigurationProvider).AsNoTracking()
                .ToListAsync(cancellationToken);

            return OperationResult<List<BrokerDto>>.Success(brokers);
        }
    }
}