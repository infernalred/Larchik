using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetInstrument;

public record GetInstrumentQuery(Guid Id) : IRequest<Result<InstrumentDto?>>;
