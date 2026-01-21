using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Instruments.EditInstrument;

public record EditInstrumentCommand(Guid Id, InstrumentModel Model) : IRequest<Result<Unit>>;
