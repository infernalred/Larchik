using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions;

public static class InstrumentCorporateActionWriteHelper
{
    public static string? Validate(InstrumentCorporateActionModel model)
    {
        if (!InstrumentCorporateActionRules.IsSupportedType(model.Type))
        {
            return "Only split and reverse split are supported as instrument corporate actions.";
        }

        if (model.Factor <= 0)
        {
            return "Split factor must be greater than 0.";
        }

        if (model.Factor == 1m)
        {
            return "Split factor must be different from 1.";
        }

        if (model.EffectiveDate.Offset != TimeSpan.Zero)
        {
            return "EffectiveDate must be in UTC (ISO format with 'Z').";
        }

        if (string.IsNullOrWhiteSpace(model.Note))
        {
            return "Note is required.";
        }

        if (model.Note.Trim().Length > 500)
        {
            return "Note must be 500 characters or fewer.";
        }

        return null;
    }

    public static NormalizedInstrumentCorporateActionInput Normalize(InstrumentCorporateActionModel model) =>
        new(
            model.Type,
            model.Factor,
            InstrumentCorporateActionRules.NormalizeEffectiveDate(model.EffectiveDate),
            model.Note.Trim());

    public static async Task<bool> HasDuplicateAsync(
        LarchikContext context,
        Guid instrumentId,
        NormalizedInstrumentCorporateActionInput input,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        return await context.InstrumentCorporateActions
            .AnyAsync(x =>
                    x.Id != excludeId &&
                    x.InstrumentId == instrumentId &&
                    x.Type == input.Type &&
                    x.EffectiveDate == input.EffectiveDate,
                cancellationToken);
    }

    public static async Task ScheduleAffectedPortfoliosRebuildAsync(
        LarchikContext context,
        IPortfolioRecalcService recalc,
        Guid instrumentId,
        DateTime fromDate,
        CancellationToken cancellationToken)
    {
        var portfolioIds = await context.Operations
            .Where(x => x.InstrumentId == instrumentId)
            .Select(x => x.PortfolioId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var portfolioId in portfolioIds)
        {
            await recalc.ScheduleRebuild(portfolioId, fromDate, cancellationToken);
        }
    }
}

public sealed record NormalizedInstrumentCorporateActionInput(
    OperationType Type,
    decimal Factor,
    DateTime EffectiveDate,
    string Note);
