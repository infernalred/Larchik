using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Instruments.GetInstrument;

public record GetInstrumentQuery(Guid Id) : IRequest<Result<InstrumentDto?>>;
