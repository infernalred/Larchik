using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.EditOperation;

public class EditOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<EditOperationCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditOperationCommand request, CancellationToken cancellationToken)
    {
        if (OperationTypeRules.IsAdministrativeCorporateAction(request.Model.Type))
        {
            return Result<Unit>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .Include(o => o.Portfolio)
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.Portfolio != null && o.Portfolio.UserId == userId, cancellationToken);

        if (op is null) return null;
        if (OperationTypeRules.IsAdministrativeCorporateAction(op.Type))
        {
            return Result<Unit>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var originalTradeDate = op.TradeDate;
        var instrumentId = request.Model.InstrumentId;
        var requiresInstrument = OperationTypeRules.RequiresInstrument(request.Model.Type);
        if (requiresInstrument && instrumentId is null)
        {
            return Result<Unit>.Failure("Instrument is required for selected operation type.");
        }

        Instrument? instrument = null;
        if (instrumentId is not null)
        {
            instrument = await context.Instruments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == instrumentId.Value, cancellationToken);

            if (instrument is null)
            {
                return Result<Unit>.Failure("Selected instrument was not found.");
            }
        }

        var tradeDate = OperationInputNormalizer.NormalizeUtc(request.Model.TradeDate);
        var settlementDate = OperationInputNormalizer.NormalizeUtc(request.Model.SettlementDate) ?? tradeDate;
        var brokerCode = await context.Brokers
            .AsNoTracking()
            .Where(x => x.Id == op.Portfolio!.BrokerId)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(cancellationToken);

        op.InstrumentId = instrumentId;
        op.Type = request.Model.Type;
        op.Quantity = request.Model.Quantity;
        op.Price = request.Model.Price;
        op.Fee = request.Model.Fee;
        op.CurrencyId = request.Model.CurrencyId.ToUpperInvariant();
        op.TradeDate = tradeDate;
        op.SettlementDate = settlementDate;
        op.Note = request.Model.Note;
        if (!BrokerOperationIdentityHelper.IsConfirmedImportedKey(op.BrokerOperationKey))
        {
            var canonicalInstrumentCode = instrument is null
                ? null
                : !string.IsNullOrWhiteSpace(instrument.Isin)
                    ? instrument.Isin
                    : instrument.Ticker;

            op.BrokerOperationKey = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
                context,
                op.PortfolioId,
                brokerCode,
                op,
                canonicalInstrumentCode,
                op.Id,
                cancellationToken);
        }

        op.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var fromDate = originalTradeDate < op.TradeDate ? originalTradeDate : op.TradeDate;
        await recalc.ScheduleRebuild(op.PortfolioId, fromDate, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
