﻿using Larchik.Application.Helpers;
using Larchik.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Deals;

public class Delete
{
    public class Command : IRequest<OperationResult<Unit>>
    {
        public Guid AccountId { get; set; }
        public Guid DealId { get; set; }
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
            return await _dealService.DeleteDeal(request.AccountId, request.DealId, cancellationToken);
        }
    }
}