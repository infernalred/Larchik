using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Larchik.Application.Operations;

internal static class OperationWriteHelper
{
    private const string BrokerOperationKeyConstraintName = "ix_operations_portfolio_id_broker_operation_key";

    public static async Task<Result<ResolvedOperationInput>> ResolveInputAsync(
        LarchikContext context,
        OperationModel model,
        CancellationToken cancellationToken)
    {
        var requiresInstrument = OperationTypeRules.RequiresInstrument(model.Type);
        var instrumentId = requiresInstrument ? model.InstrumentId : null;
        if (requiresInstrument && instrumentId is null)
        {
            return Result<ResolvedOperationInput>.Failure("Instrument is required for selected operation type.");
        }

        var currencyId = OperationInputNormalizer.NormalizeCurrencyId(model.CurrencyId);
        if (currencyId is null)
        {
            return Result<ResolvedOperationInput>.Failure("Currency must be a 3-letter code.");
        }

        ResolvedInstrument? instrument = null;
        if (instrumentId is not null)
        {
            instrument = await context.Instruments
                .AsNoTracking()
                .Where(x => x.Id == instrumentId.Value)
                .Select(x => new ResolvedInstrument(x.Id, x.Isin, x.Ticker))
                .FirstOrDefaultAsync(cancellationToken);

            if (instrument is null)
            {
                return Result<ResolvedOperationInput>.Failure("Selected instrument was not found.");
            }
        }

        var tradeDate = OperationInputNormalizer.NormalizeUtc(model.TradeDate);
        var settlementDate = OperationInputNormalizer.NormalizeUtc(model.SettlementDate) ?? tradeDate;

        return Result<ResolvedOperationInput>.Success(new ResolvedOperationInput(
            InstrumentId: instrumentId,
            CurrencyId: currencyId,
            TradeDate: tradeDate,
            SettlementDate: settlementDate,
            Note: OperationInputNormalizer.NormalizeNote(model.Note),
            CanonicalInstrumentCode: instrument is null
                ? null
                : OperationInputNormalizer.NormalizeInstrumentCode(instrument.Isin, instrument.Ticker)));
    }

    public static void Apply(Operation operation, OperationModel model, ResolvedOperationInput input, DateTime now)
    {
        operation.InstrumentId = input.InstrumentId;
        operation.Type = model.Type;
        operation.Quantity = model.Quantity;
        operation.Price = model.Price;
        operation.Fee = model.Fee;
        operation.CurrencyId = input.CurrencyId;
        operation.TradeDate = input.TradeDate;
        operation.SettlementDate = input.SettlementDate;
        operation.Note = input.Note;
        operation.UpdatedAt = now;
    }

    public static bool IsBrokerOperationKeyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BrokerOperationKeyConstraintName
        };

    internal sealed record ResolvedOperationInput(
        Guid? InstrumentId,
        string CurrencyId,
        DateTime TradeDate,
        DateTime SettlementDate,
        string? Note,
        string? CanonicalInstrumentCode);

    private sealed record ResolvedInstrument(Guid Id, string? Isin, string? Ticker);
}
