using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Models;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateStockCommandHandler(DataContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreateStockCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();

        var stock = new Stock
        {
            Ticker = request.Stock.Ticker,
            Figi = request.Stock.Figi,
            CompanyName = request.Stock.CompanyName,
            TypeId = request.Stock.Type,
            CurrencyId = request.Stock.Currency,
            SectorId = request.Stock.Sector,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        await context.Stock.AddAsync(stock, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}