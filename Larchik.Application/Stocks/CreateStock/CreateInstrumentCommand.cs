using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Instruments.CreateInstrument;

public record CreateInstrumentCommand(InstrumentModel Model) : IRequest<Result<Unit>>;
