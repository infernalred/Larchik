using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.BrokerAccounts;

public class Details
{
    public class Query : IRequest<OperationResult<BrokerAccountDto>>
    {
        public Guid Id { get; set; }
    }

    public class Handler : IRequestHandler<Query, OperationResult<BrokerAccountDto>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly IUserAccessor _userAccessor;

        public Handler(ILogger<Handler> logger, DataContext context, IMapper mapper, IUserAccessor userAccessor)
        {
            _logger = logger;
            _context = context;
            _mapper = mapper;
            _userAccessor = userAccessor;
        }
        
        public async Task<OperationResult<BrokerAccountDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var account = await _context.Accounts
                .Include(x => x.Broker)
                .Include(x => x.Assets)
                .Include(x => x.Cash).AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.Id && x.User.UserName == _userAccessor.GetUsername(), cancellationToken);
            
            if (account == null) return null;
            
            return OperationResult<BrokerAccountDto>.Success(_mapper.Map<BrokerAccountDto>(account));
        }
    }
}