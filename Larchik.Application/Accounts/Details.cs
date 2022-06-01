using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Accounts;

public class Details
{
    public class Query : IRequest<OperationResult<AccountDto>>
    {
        public Guid Id { get; set; }
    }

    public class Handler : IRequestHandler<Query, OperationResult<AccountDto>>
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
        
        public async Task<OperationResult<AccountDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var account = await _context.Accounts
                .Include(x => x.Assets.Where(a => a.Quantity != 0))
                .ThenInclude(a => a.Stock)
                .Include(x => x.Deals)
                .SingleOrDefaultAsync(x => x.Id == request.Id && x.User.UserName == _userAccessor.GetUsername(), cancellationToken);
            
            return OperationResult<AccountDto>.Success(_mapper.Map<AccountDto>(account));
        }
    }
}