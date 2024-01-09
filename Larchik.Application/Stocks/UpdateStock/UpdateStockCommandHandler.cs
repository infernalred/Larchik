using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.UpdateStock;

public class UpdateStockCommandHandler(DataContext context, IUserAccessor userAccessor, IMapperBase mapper)
    : IRequestHandler<UpdateStockCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(UpdateStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await context.Stock
            .FirstOrDefaultAsync(x => x.Ticker == request.Stock.Ticker, cancellationToken);

        if (stock is null) return null;

        mapper.Map(request.Stock, stock);

        stock.UpdatedBy = userAccessor.GetUserId();

        var result = await context.SaveChangesAsync(cancellationToken) > 0;

        return !result ? Result<Unit>.Failure("Failed to update stock") : Result<Unit>.Success(Unit.Value);
    }
}