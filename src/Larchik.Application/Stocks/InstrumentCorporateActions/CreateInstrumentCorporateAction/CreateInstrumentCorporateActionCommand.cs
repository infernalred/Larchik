using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;

public record CreateInstrumentCorporateActionCommand(Guid InstrumentId, InstrumentCorporateActionModel Model)
    : IRequest<Result<Guid>>;
