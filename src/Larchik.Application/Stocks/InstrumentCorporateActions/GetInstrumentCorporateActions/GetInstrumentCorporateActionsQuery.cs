using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.GetInstrumentCorporateActions;

public record GetInstrumentCorporateActionsQuery(Guid InstrumentId)
    : IRequest<Result<IReadOnlyCollection<InstrumentCorporateActionDto>>>;
