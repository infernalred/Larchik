using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;

public record EditInstrumentCorporateActionCommand(Guid InstrumentId, Guid Id, InstrumentCorporateActionModel Model)
    : IRequest<Result<Unit>?>;
