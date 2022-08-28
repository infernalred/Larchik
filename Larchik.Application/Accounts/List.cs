using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Accounts;

public class List
{
    public class Query : IRequest<OperationResult<List<AccountDto>>>{}
    
    public class Handler : IRequestHandler<Query, OperationResult<List<AccountDto>>>
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
        
        public async Task<OperationResult<List<AccountDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var accounts = await _context.Accounts
                .Where(x => x.User.UserName == _userAccessor.GetUsername())
                .Include(x => x.Assets.Where(a => a.Quantity != 0))
                .ThenInclude(s => s.Stock)
                .ToListAsync(cancellationToken);
            
            return OperationResult<List<AccountDto>>.Success(_mapper.Map<List<AccountDto>>(accounts));
        }
    }
}