using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.DeleteInstrumentCorporateAction;

public record DeleteInstrumentCorporateActionCommand(Guid InstrumentId, Guid Id)
    : IRequest<Result<Unit>>;
