using Larchik.Application.Contracts;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Larchik.Application.Stocks.InstrumentCorporateActions;

public static class InstrumentCorporateActionWriteHelper
{
    private const string CorporateActionUniqueConstraintName = "ix_instrument_corporate_actions_instrument_id_type_effective_d";
    private const string DuplicateMessage = "A corporate action with the same type and effective date already exists.";

    public static string? Validate(InstrumentCorporateActionModel model, InstrumentType instrumentType)
    {
        if (!InstrumentCorporateActionRules.IsSupportedType(model.Type))
        {
            return "Only split and reverse split are supported as instrument corporate actions.";
        }

        if (!InstrumentCorporateActionRules.IsSupportedInstrumentType(instrumentType))
        {
            return "Corporate actions are supported only for Equity and Etf instruments.";
        }

        if (!InstrumentCorporateActionRules.IsValidFactor(model.Type, model.Factor))
        {
            return model.Type == OperationType.Split
                ? "Split factor must be greater than 1."
                : "Reverse split factor must be greater than 0 and less than 1.";
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

    public static bool IsDuplicateConflict(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: not null
            } pg &&
            (string.Equals(pg.ConstraintName, CorporateActionUniqueConstraintName, StringComparison.OrdinalIgnoreCase) ||
             pg.ConstraintName.StartsWith("ix_instrument_corporate_actions_instrument_id_type_effective", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var innerMessage = exception.InnerException?.Message;
        if (string.IsNullOrWhiteSpace(innerMessage))
        {
            return false;
        }

        var normalized = innerMessage.ToLowerInvariant();
        return normalized.Contains("unique constraint failed", StringComparison.Ordinal) &&
               normalized.Contains("instrument", StringComparison.Ordinal) &&
               normalized.Contains("type", StringComparison.Ordinal) &&
               normalized.Contains("effective", StringComparison.Ordinal);
    }

    public static string DuplicateErrorMessage => DuplicateMessage;

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
