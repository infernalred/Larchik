using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.GetOperation;

public class GetOperationQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetOperationQuery, Result<OperationDto?>>
{
    public async Task<Result<OperationDto?>> Handle(GetOperationQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .AsNoTracking()
            .Where(x =>
                x.Id == request.Id &&
                x.Portfolio != null &&
                x.Portfolio.UserId == userId &&
                x.Type != OperationType.Split &&
                x.Type != OperationType.ReverseSplit)
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
            .FirstOrDefaultAsync(cancellationToken);

        return Result<OperationDto?>.Success(op);
    }
}
