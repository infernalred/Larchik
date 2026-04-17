using Larchik.Application.Contracts;
using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.GetOperations;

public class GetOperationsQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetOperationsQuery, Result<PagedResult<OperationDto>>>
{
    private const int MaxPageSize = 200;

    public async Task<Result<PagedResult<OperationDto>>> Handle(GetOperationsQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var result = await context.Operations
            .AsNoTracking()
            .Where(x =>
                x.PortfolioId == request.PortfolioId &&
                x.Portfolio != null &&
                x.Portfolio.UserId == userId &&
                x.Type != OperationType.Split &&
                x.Type != OperationType.ReverseSplit)
            .OrderByDescending(x => x.TradeDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new OperationDto
            {
                Id = x.Id,
                PortfolioId = x.PortfolioId,
                InstrumentId = x.InstrumentId,
                InstrumentTicker = x.Instrument != null ? x.Instrument.Ticker : null,
                Type = x.Type,
                Quantity = x.Quantity,
                Price = x.Price,
                Fee = x.Fee,
                CurrencyId = x.CurrencyId,
                TradeDate = x.TradeDate,
                SettlementDate = x.SettlementDate,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<OperationDto>>.Success(result);
    }
}
