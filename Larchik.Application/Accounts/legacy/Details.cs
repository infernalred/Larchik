// using Larchik.Application.Contracts;
// using Larchik.Application.Dtos;
// using Larchik.Application.Helpers;
// using Larchik.Persistence.Context;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Larchik.Application.Accounts;
//
// public class Details
// {
//     public class Query : IRequest<Result<AccountDto>>
//     {
//         public Guid Id { get; set; }
//     }
//
//     public class Handler : IRequestHandler<Query, Result<AccountDto>>
//     {
//         private readonly ILogger<Handler> _logger;
//         private readonly DataContext _context;
//         private readonly IUserAccessor _userAccessor;
//
//         public Handler(ILogger<Handler> logger, DataContext context, IUserAccessor userAccessor)
//         {
//             _logger = logger;
//             _context = context;
//             _userAccessor = userAccessor;
//         }
//         
//         public async Task<Result<AccountDto>> Handle(Query request, CancellationToken cancellationToken)
//         {
//             var account = await _context.Accounts
//                 .Include(x => x.Assets.Where(a => a.Quantity != 0))
//                 .ThenInclude(a => a.Stock)
//                 .SingleOrDefaultAsync(x => x.Id == request.Id && x.UserId == _userAccessor.GetUserId(), cancellationToken)
//                 ;
//             
//             return Result<AccountDto>.Success();
//         }
//     }
// }