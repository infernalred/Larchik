using Larchik.Application.Models;

namespace Larchik.Application.Stocks.EditStock;

public record EditInstrumentCommand(Guid Id, InstrumentModel Model);
