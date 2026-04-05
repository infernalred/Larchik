using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.EditStock;

public record EditInstrumentCommand(Guid Id, InstrumentModel Model) : IRequest<Result<Unit>>;
