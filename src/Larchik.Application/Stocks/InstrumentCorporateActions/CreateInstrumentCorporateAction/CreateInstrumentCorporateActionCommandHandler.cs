using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
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
        var validationError = InstrumentCorporateActionWriteHelper.Validate(request.Model);
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

        var input = InstrumentCorporateActionWriteHelper.Normalize(request.Model);

        var duplicateExists = await InstrumentCorporateActionWriteHelper.HasDuplicateAsync(
            context,
            request.InstrumentId,
            input,
            excludeId: null,
            cancellationToken);

        if (duplicateExists)
        {
            return Result<Guid>.Failure("A corporate action with the same type and effective date already exists.");
        }

        var entity = new InstrumentCorporateAction
        {
            Id = Guid.NewGuid(),
            InstrumentId = request.InstrumentId,
            Type = input.Type,
            Factor = input.Factor,
            EffectiveDate = input.EffectiveDate,
            Note = input.Note
        };

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.InstrumentCorporateActions.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await InstrumentCorporateActionWriteHelper.ScheduleAffectedPortfoliosRebuildAsync(
            context,
            recalc,
            request.InstrumentId,
            input.EffectiveDate,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<Guid>.Success(entity.Id);
    }
}
