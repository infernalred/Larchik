using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;

public class CreateInstrumentCorporateActionCommandHandler(LarchikContext context, IPortfolioRecalcService recalc)
    : IRequestHandler<CreateInstrumentCorporateActionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInstrumentCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request.Model);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
        }

        var instrument = await context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.InstrumentId, cancellationToken);

        if (instrument is null)
        {
            return Result<Guid>.Failure("Instrument not found.");
        }

        var effectiveDate = InstrumentCorporateActionRules.NormalizeEffectiveDate(request.Model.EffectiveDate);
        var note = request.Model.Note.Trim();

        var duplicateExists = await context.InstrumentCorporateActions
            .AsNoTracking()
            .AnyAsync(x =>
                x.InstrumentId == request.InstrumentId &&
                x.Type == request.Model.Type &&
                x.EffectiveDate == effectiveDate,
                cancellationToken);

        if (duplicateExists)
        {
            return Result<Guid>.Failure("A corporate action with the same type and effective date already exists.");
        }

        var entity = new InstrumentCorporateAction
        {
            Id = Guid.NewGuid(),
            InstrumentId = request.InstrumentId,
            Type = request.Model.Type,
            Factor = request.Model.Factor,
            EffectiveDate = effectiveDate,
            Note = note
        };

        await context.InstrumentCorporateActions.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await ScheduleAffectedPortfoliosRebuildAsync(context, recalc, request.InstrumentId, effectiveDate, cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }

    internal static string? Validate(InstrumentCorporateActionModel model)
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

    internal static async Task ScheduleAffectedPortfoliosRebuildAsync(
        LarchikContext context,
        IPortfolioRecalcService recalc,
        Guid instrumentId,
        DateTime fromDate,
        CancellationToken cancellationToken)
    {
        var portfolioIds = await context.Operations
            .AsNoTracking()
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
