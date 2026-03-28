using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.CreateOperation;

public class CreateOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<CreateOperationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOperationCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .AsNoTracking()
            .Include(x => x.Broker)
            .FirstOrDefaultAsync(x => x.Id == request.PortfolioId && x.UserId == userId, cancellationToken);

        if (portfolio is null) return Result<Guid>.Failure("Portfolio not found");

        var instrumentId = request.Model.InstrumentId;
        var requiresInstrument = OperationTypeRules.RequiresInstrument(request.Model.Type);
        if (requiresInstrument && instrumentId is null)
        {
            return Result<Guid>.Failure("Instrument is required for selected operation type.");
        }

        Instrument? instrument = null;
        if (instrumentId is not null)
        {
            instrument = await context.Instruments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == instrumentId.Value, cancellationToken);

            if (instrument is null)
            {
                return Result<Guid>.Failure("Selected instrument was not found.");
            }
        }

        var tradeDate = OperationInputNormalizer.NormalizeUtc(request.Model.TradeDate);
        var settlementDate = OperationInputNormalizer.NormalizeUtc(request.Model.SettlementDate) ?? tradeDate;

        var entity = new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = request.PortfolioId,
            InstrumentId = instrumentId,
            Type = request.Model.Type,
            Quantity = request.Model.Quantity,
            Price = request.Model.Price,
            Fee = request.Model.Fee,
            CurrencyId = request.Model.CurrencyId.ToUpperInvariant(),
            TradeDate = tradeDate,
            SettlementDate = settlementDate,
            Note = request.Model.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var canonicalInstrumentCode = instrument is null
            ? null
            : !string.IsNullOrWhiteSpace(instrument.Isin)
                ? instrument.Isin
                : instrument.Ticker;

        entity.BrokerOperationKey = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
            context,
            request.PortfolioId,
            portfolio.Broker?.Code,
            entity,
            canonicalInstrumentCode,
            excludeOperationId: null,
            cancellationToken);

        await context.Operations.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await recalc.ScheduleRebuild(request.PortfolioId, entity.TradeDate, cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }
}
