using Larchik.Application.Stocks.InstrumentCorporateActions;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Helpers;

public static class InstrumentCorporateActionOperationMerger
{
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
            .AsNoTracking()
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

        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .ToHashSet();

        var relevantActions = corporateActions
            .Where(x =>
                instrumentIds.Contains(x.InstrumentId) &&
                operations.Any(op =>
                    op.InstrumentId == x.InstrumentId &&
                    op.TradeDate.Date < x.EffectiveDate.Date))
            .ToArray();

        if (relevantActions.Length == 0)
        {
            return operations;
        }

        var actionKeys = relevantActions
            .Select(ToKey)
            .ToHashSet();

        var portfolioId = operations[0].PortfolioId;
        var merged = new List<Operation>(operations.Count + relevantActions.Length);

        foreach (var operation in operations)
        {
            if (IsLegacyCorporateActionOperation(operation, actionKeys))
            {
                continue;
            }

            merged.Add(operation);
        }

        foreach (var action in relevantActions)
        {
            if (!instruments.TryGetValue(action.InstrumentId, out var instrument))
            {
                continue;
            }

            merged.Add(new Operation
            {
                Id = action.Id,
                PortfolioId = portfolioId,
                InstrumentId = action.InstrumentId,
                Type = action.Type,
                Quantity = action.Factor,
                Price = 0,
                Fee = 0,
                CurrencyId = instrument.CurrencyId,
                TradeDate = DateTime.SpecifyKind(action.EffectiveDate.Date, DateTimeKind.Utc),
                SettlementDate = DateTime.SpecifyKind(action.EffectiveDate.Date, DateTimeKind.Utc),
                Note = action.Note,
                CreatedAt = CorporateActionCreatedAt,
                UpdatedAt = CorporateActionCreatedAt
            });
        }

        return merged
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToList();
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

    private readonly record struct CorporateActionKey(
        Guid InstrumentId,
        OperationType Type,
        DateTime EffectiveDate,
        decimal Factor);
}
