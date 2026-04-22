using Larchik.Application.Stocks.InstrumentCorporateActions;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Helpers;

public static class InstrumentCorporateActionOperationMerger
{
    // Corporate actions are merged as synthetic end-of-day operations, after user/imported operations on the same date.
    private static readonly DateTime CorporateActionCreatedAt = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    public static async Task<IReadOnlyList<InstrumentCorporateAction>> LoadAsync(
        LarchikContext context,
        IEnumerable<Guid> instrumentIds,
        CancellationToken cancellationToken)
    {
        var ids = instrumentIds
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return await context.InstrumentCorporateActions
            .Where(x =>
                ids.Contains(x.InstrumentId) &&
                (x.Type == OperationType.Split || x.Type == OperationType.ReverseSplit))
            .OrderBy(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);
    }

    public static IReadOnlyList<Operation> Merge(
        IReadOnlyList<Operation> operations,
        IReadOnlyCollection<InstrumentCorporateAction> corporateActions,
        IReadOnlyDictionary<Guid, Instrument> instruments)
    {
        if (operations.Count == 0 || corporateActions.Count == 0)
        {
            return operations;
        }

        var earliestTradeDateByInstrument = operations
            .Where(x => x.InstrumentId is not null)
            .GroupBy(x => x.InstrumentId!.Value)
            .ToDictionary(x => x.Key, x => x.Min(y => y.TradeDate.Date));

        var relevantActions = corporateActions
            .Where(x =>
                earliestTradeDateByInstrument.TryGetValue(x.InstrumentId, out var earliestTradeDate) &&
                earliestTradeDate < x.EffectiveDate.Date)
            .ToArray();

        if (relevantActions.Length == 0)
        {
            return operations;
        }

        var actionKeys = relevantActions
            .Select(ToKey)
            .ToHashSet();

        var merged = operations
            .Where(x => !IsLegacyCorporateActionOperation(x, actionKeys))
            .Concat(BuildSyntheticOperations(relevantActions, instruments, operations[0].PortfolioId))
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToList();

        return merged;
    }

    private static bool IsLegacyCorporateActionOperation(
        Operation operation,
        IReadOnlySet<CorporateActionKey> actionKeys)
    {
        if (operation.InstrumentId is null || !InstrumentCorporateActionRules.IsSupportedType(operation.Type))
        {
            return false;
        }

        return actionKeys.Contains(new CorporateActionKey(
            operation.InstrumentId.Value,
            operation.Type,
            operation.TradeDate.Date,
            operation.Quantity));
    }

    private static CorporateActionKey ToKey(InstrumentCorporateAction action) =>
        new(action.InstrumentId, action.Type, action.EffectiveDate.Date, action.Factor);

    private static IEnumerable<Operation> BuildSyntheticOperations(
        IEnumerable<InstrumentCorporateAction> actions,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        Guid portfolioId) =>
        actions
            .Where(x => instruments.ContainsKey(x.InstrumentId))
            .Select(action =>
            {
                var instrument = instruments[action.InstrumentId];
                var effectiveDateUtc = DateTime.SpecifyKind(action.EffectiveDate.Date, DateTimeKind.Utc);

                return new Operation
                {
                    Id = action.Id,
                    PortfolioId = portfolioId,
                    InstrumentId = action.InstrumentId,
                    Type = action.Type,
                    Quantity = action.Factor,
                    Price = 0,
                    Fee = 0,
                    CurrencyId = instrument.CurrencyId,
                    TradeDate = effectiveDateUtc,
                    SettlementDate = effectiveDateUtc,
                    Note = action.Note,
                    CreatedAt = CorporateActionCreatedAt,
                    UpdatedAt = CorporateActionCreatedAt
                };
            });

    private readonly record struct CorporateActionKey(
        Guid InstrumentId,
        OperationType Type,
        DateTime EffectiveDate,
        decimal Factor);
}
