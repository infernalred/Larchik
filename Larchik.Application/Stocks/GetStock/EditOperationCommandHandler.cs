using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class EditOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<EditOperationCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditOperationCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .Include(o => o.Portfolio)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.Portfolio != null && o.Portfolio.UserId == userId, cancellationToken);

        if (op is null) return null;

        op.InstrumentId = request.Model.InstrumentId;
        op.Type = request.Model.Type;
        op.Quantity = request.Model.Quantity;
        op.Price = request.Model.Price;
        op.Fee = request.Model.Fee;
        op.CurrencyId = request.Model.CurrencyId.ToUpperInvariant();
        op.TradeDate = request.Model.TradeDate;
        op.SettlementDate = request.Model.SettlementDate;
        op.Note = request.Model.Note;
        op.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        // TODO: trigger snapshot/valuation recalculation for this portfolio.

        return Result<Unit>.Success(Unit.Value);
    }
}
