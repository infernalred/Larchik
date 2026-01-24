using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class CreateOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<CreateOperationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOperationCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PortfolioId && x.UserId == userId, cancellationToken);

        if (portfolio is null) return Result<Guid>.Failure("Portfolio not found");

        var entity = new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = request.PortfolioId,
            InstrumentId = request.Model.InstrumentId,
            Type = request.Model.Type,
            Quantity = request.Model.Quantity,
            Price = request.Model.Price,
            Fee = request.Model.Fee,
            CurrencyId = request.Model.CurrencyId.ToUpperInvariant(),
            TradeDate = request.Model.TradeDate,
            SettlementDate = request.Model.SettlementDate,
            Note = request.Model.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Operations.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await recalc.ScheduleRebuild(request.PortfolioId, entity.TradeDate, cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }
}
