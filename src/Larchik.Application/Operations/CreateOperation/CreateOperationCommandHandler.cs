using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Larchik.Application.Operations.CreateOperation;

public class CreateOperationCommandHandler(LarchikContext context, IUserAccessor userAccessor, IPortfolioRecalcService recalc)
    : IRequestHandler<CreateOperationCommand, Result<Guid>>
{
    private const string BrokerOperationKeyConstraintName = "ix_operations_portfolio_id_broker_operation_key";

    public async Task<Result<Guid>> Handle(CreateOperationCommand request, CancellationToken cancellationToken)
    {
        if (OperationTypeRules.IsAdministrativeCorporateAction(request.Model.Type))
        {
            return Result<Guid>.Failure("Split and reverse split must be managed as administrative corporate actions.");
        }

        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .AsNoTracking()
            .Where(x => x.Id == request.PortfolioId && x.UserId == userId)
            .Select(x => new PortfolioIdentity(x.Id, x.Broker == null ? null : x.Broker.Code))
            .FirstOrDefaultAsync(cancellationToken);

        if (portfolio is null) return Result<Guid>.Failure("Portfolio not found");

        var requiresInstrument = OperationTypeRules.RequiresInstrument(request.Model.Type);
        var instrumentId = requiresInstrument ? request.Model.InstrumentId : null;
        if (requiresInstrument && instrumentId is null)
        {
            return Result<Guid>.Failure("Instrument is required for selected operation type.");
        }

        var currencyId = NormalizeCurrencyId(request.Model.CurrencyId);
        if (currencyId is null)
        {
            return Result<Guid>.Failure("Currency must be a 3-letter code.");
        }

        InstrumentIdentity? instrument = null;
        if (requiresInstrument && instrumentId is not null)
        {
            instrument = await context.Instruments
                .AsNoTracking()
                .Where(x => x.Id == instrumentId.Value)
                .Select(x => new InstrumentIdentity(x.Id, x.Isin, x.Ticker))
                .FirstOrDefaultAsync(cancellationToken);

            if (instrument is null)
            {
                return Result<Guid>.Failure("Selected instrument was not found.");
            }
        }

        var tradeDate = OperationInputNormalizer.NormalizeUtc(request.Model.TradeDate);
        var settlementDate = OperationInputNormalizer.NormalizeUtc(request.Model.SettlementDate) ?? tradeDate;
        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(request.Model.Note)
            ? null
            : request.Model.Note.Trim();

        var entity = new Operation
        {
            Id = Guid.NewGuid(),
            PortfolioId = request.PortfolioId,
            InstrumentId = instrumentId,
            Type = request.Model.Type,
            Quantity = request.Model.Quantity,
            Price = request.Model.Price,
            Fee = request.Model.Fee,
            CurrencyId = currencyId,
            TradeDate = tradeDate,
            SettlementDate = settlementDate,
            Note = note,
            CreatedAt = now,
            UpdatedAt = now
        };

        var canonicalInstrumentCode = NormalizeInstrumentCode(instrument);

        entity.BrokerOperationKey = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
            context,
            request.PortfolioId,
            portfolio.BrokerCode,
            entity,
            canonicalInstrumentCode,
            excludeOperationId: null,
            cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Operations.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await recalc.ScheduleRebuild(request.PortfolioId, entity.TradeDate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsBrokerOperationKeyConflict(ex))
        {
            return Result<Guid>.Failure("Operation with the same broker identity already exists. Please retry the request.");
        }

        return Result<Guid>.Success(entity.Id);
    }

    private static string? NormalizeCurrencyId(string? currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            return null;
        }

        var normalized = currencyId.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }

    private static string? NormalizeInstrumentCode(InstrumentIdentity? instrument)
    {
        var rawCode = !string.IsNullOrWhiteSpace(instrument?.Isin)
            ? instrument.Isin
            : instrument?.Ticker;

        return string.IsNullOrWhiteSpace(rawCode)
            ? null
            : rawCode.Trim().ToUpperInvariant();
    }

    private static bool IsBrokerOperationKeyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BrokerOperationKeyConstraintName
        };

    private sealed record PortfolioIdentity(Guid Id, string? BrokerCode);
    private sealed record InstrumentIdentity(Guid Id, string? Isin, string? Ticker);
}
