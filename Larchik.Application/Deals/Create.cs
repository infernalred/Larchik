﻿using FluentValidation;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Application.Services.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Deals;

public class Create
{
    public class Command : IRequest<OperationResult<Unit>>
    {
        public DealDto Deal { get; set; } = null!;
    }
    
    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Deal).SetValidator(new DealValidator());
        }
    }
    
    public class Handler : IRequestHandler<Command, OperationResult<Unit>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly IDealService _dealService;

        public Handler(ILogger<Handler> logger, IDealService dealService)
        {
            _logger = logger;
            _dealService = dealService;
        }
        
        public async Task<OperationResult<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {
            return await _dealService.CreateDeal(request.Deal, cancellationToken);
        }
    }
}