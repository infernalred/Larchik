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
            Id = Guid.NewGuid(),
            Ticker = request.Stock.Ticker,
            Isin = request.Stock.Isin,
            Name = request.Stock.Name,
            Kind = request.Stock.Kind,
            CurrencyId = request.Stock.CurrencyId,
            SectorId = request.Stock.SectorId,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        await context.Stocks.AddAsync(stock, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}