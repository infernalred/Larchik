using AutoMapper;
using FluentValidation;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Brokers;

public class Create
{
    public class Command : IRequest<OperationResult<BrokerDto>>
    {
        public BrokerDto Broker { get; set; }
    }

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Broker).SetValidator(new BrokerValidator());
        }
    }

    public class Handler : IRequestHandler<Command, OperationResult<BrokerDto>>
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
        
        public async Task<OperationResult<BrokerDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            var broker = await _context.Brokers.FirstOrDefaultAsync(x => x.Name == request.Broker.Name, cancellationToken);

            if (broker != null) return null;

            broker = _mapper.Map<BrokerDto, Broker>(request.Broker);

            _context.Add(broker);

            await _context.SaveChangesAsync(cancellationToken);
            
            return OperationResult<BrokerDto>.Success(_mapper.Map<Broker, BrokerDto>(broker));
        }
    }
}