using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;

public class EditInstrumentCorporateActionCommandHandler(LarchikContext context, IPortfolioRecalcService recalc)
    : IRequestHandler<EditInstrumentCorporateActionCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditInstrumentCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var validationError = CreateInstrumentCorporateActionCommandHandler.Validate(request.Model);
        if (validationError is not null)
        {
            return Result<Unit>.Failure(validationError);
        }

        var entity = await context.InstrumentCorporateActions
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.InstrumentId == request.InstrumentId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var effectiveDate = InstrumentCorporateActionRules.NormalizeEffectiveDate(request.Model.EffectiveDate);
        var note = request.Model.Note.Trim();
        var duplicateExists = await context.InstrumentCorporateActions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != request.Id &&
                x.InstrumentId == request.InstrumentId &&
                x.Type == request.Model.Type &&
                x.EffectiveDate == effectiveDate,
                cancellationToken);

        if (duplicateExists)
        {
            return Result<Unit>.Failure("A corporate action with the same type and effective date already exists.");
        }

        var rebuildFrom = entity.EffectiveDate < effectiveDate
            ? entity.EffectiveDate
            : effectiveDate;

        entity.Type = request.Model.Type;
        entity.Factor = request.Model.Factor;
        entity.EffectiveDate = effectiveDate;
        entity.Note = note;

        await context.SaveChangesAsync(cancellationToken);
        await CreateInstrumentCorporateActionCommandHandler.ScheduleAffectedPortfoliosRebuildAsync(
            context,
            recalc,
            request.InstrumentId,
            rebuildFrom,
            cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
