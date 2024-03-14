using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Accounts;

public class List
{
    public class Query : IRequest<Result<List<AccountDto>>>{}
    
    public class Handler : IRequestHandler<Query, Result<List<AccountDto>>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        private readonly IUserAccessor _userAccessor;

        public Handler(ILogger<Handler> logger, DataContext context, IUserAccessor userAccessor)
        {
            _logger = logger;
            _context = context;
            _userAccessor = userAccessor;
        }
        
        public async Task<Result<List<AccountDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var accounts = await _context.Accounts
                .Where(x => x.UserId == _userAccessor.GetUserId())
                .Include(x => x.Assets.Where(a => a.Quantity != 0))
                .ThenInclude(s => s.Stock)
                .ProjectToType<AccountDto>()
                .ToListAsync(cancellationToken);
            
            return Result<List<AccountDto>>.Success(accounts);
        }
    }
}