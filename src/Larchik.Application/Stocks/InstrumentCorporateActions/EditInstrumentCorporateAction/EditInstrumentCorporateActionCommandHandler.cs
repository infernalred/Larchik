using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;

public class EditInstrumentCorporateActionCommandHandler(LarchikContext context, IPortfolioRecalcService recalc)
{
    public async Task<Result<Unit>?> Handle(EditInstrumentCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var instrumentType = await context.Instruments
            .Where(x => x.Id == request.InstrumentId)
            .Select(x => (InstrumentType?)x.Type)
            .FirstOrDefaultAsync(cancellationToken);
        if (instrumentType is null)
        {
            return Result<Unit>.Failure("Instrument not found.");
        }

        var validationError = InstrumentCorporateActionWriteHelper.Validate(request.Model, instrumentType.Value);
        if (validationError is not null)
        {
            return Result<Unit>.Failure(validationError);
        }

        var entity = await context.InstrumentCorporateActions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.InstrumentId == request.InstrumentId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var input = InstrumentCorporateActionWriteHelper.Normalize(request.Model);
        var duplicateExists = await InstrumentCorporateActionWriteHelper.HasDuplicateAsync(
            context,
            request.InstrumentId,
            input,
            request.Id,
            cancellationToken);

        if (duplicateExists)
        {
            return Result<Unit>.Failure(InstrumentCorporateActionWriteHelper.DuplicateErrorMessage);
        }

        var rebuildFrom = entity.EffectiveDate < input.EffectiveDate
            ? entity.EffectiveDate
            : input.EffectiveDate;

        entity.Type = input.Type;
        entity.Factor = input.Factor;
        entity.EffectiveDate = input.EffectiveDate;
        entity.Note = input.Note;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await InstrumentCorporateActionWriteHelper.ScheduleAffectedPortfoliosRebuildAsync(
                context,
                recalc,
                request.InstrumentId,
                rebuildFrom,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (InstrumentCorporateActionWriteHelper.IsDuplicateConflict(ex))
        {
            return Result<Unit>.Failure(InstrumentCorporateActionWriteHelper.DuplicateErrorMessage);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
