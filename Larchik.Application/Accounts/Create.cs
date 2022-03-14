using AutoMapper;
using FluentValidation;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Accounts;

public class Create
{
    public class Command : IRequest<OperationResult<Unit>>
    {
        public AccountCreateDto Account { get; set; }
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
        private readonly IMapper _mapper;
        private readonly IUserAccessor _userAccessor;

        public Handler(ILogger<Handler> logger, DataContext context, IMapper mapper, IUserAccessor userAccessor)
        {
            _logger = logger;
            _context = context;
            _mapper = mapper;
            _userAccessor = userAccessor;
        }
        
        public async Task<OperationResult<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == _userAccessor.GetUsername(), cancellationToken);
            
            var broker = await _context.Brokers.FirstOrDefaultAsync(x => x.Id == request.Account.BrokerId, cancellationToken);
            
            if (broker == null) return OperationResult<Unit>.Failure("Broker doesn't exist");

            var account = new Account
            {
                Id = request.Account.Id,
                User = user!,
                Broker = broker
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
            
            return OperationResult<Unit>.Success(Unit.Value);
        }
    }
}