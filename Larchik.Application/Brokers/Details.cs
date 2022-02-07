using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Brokers;

public class Details
{
    public class Query : IRequest<OperationResult<BrokerDto>>
    {
        public int Id { get; set; }
    }
    
    public  class Handler : IRequestHandler<Query, OperationResult<BrokerDto>>
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
        
        public async Task<OperationResult<BrokerDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var broker = await _context.Brokers
                .ProjectTo<BrokerDto>(_mapper.ConfigurationProvider).AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            return OperationResult<BrokerDto>.Success(broker);
        }
    }
}