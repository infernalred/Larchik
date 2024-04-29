using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Mapster;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateStockCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreateStockCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();

        var stock = request.Model.Adapt<Stock>();
        stock.Id = Guid.NewGuid();
        stock.CreatedBy = userId;
        stock.UpdatedBy = userId;

        await context.Stocks.AddAsync(stock, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}