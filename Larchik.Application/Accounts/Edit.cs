using FluentValidation;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Accounts;

public class Edit
{
    public class Command : IRequest<OperationResult<Unit>>
    {
        public AccountCreateDto Account { get; set; } = null!;
    }
    
    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Account).SetValidator(new AccountValidator());
        }
    }
    
    public class Handler : IRequestHandler<Command, OperationResult<Unit>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        
        public Handler(ILogger<Handler> logger, DataContext context)
        {
            _logger = logger;
            _context = context;
        }
        
        public async Task<OperationResult<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {
            var account = await _context.Accounts
                .AsTracking()
                .FirstAsync(x => x.Id == request.Account.Id, cancellationToken);
            
            account.Name = request.Account.Name;

            await _context.SaveChangesAsync(cancellationToken);

            return OperationResult<Unit>.Success(Unit.Value);
        }
    }
}