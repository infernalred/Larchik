using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.EditStock;

public class EditStockCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<EditStockCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (stock is null) return null;

        request.Model.Adapt(stock);

        stock.UpdatedBy = userAccessor.GetUserId();
        stock.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}