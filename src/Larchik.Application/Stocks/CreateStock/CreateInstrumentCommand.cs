using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public record CreateInstrumentCommand(InstrumentModel Model) : IRequest<Result<Unit>>;
