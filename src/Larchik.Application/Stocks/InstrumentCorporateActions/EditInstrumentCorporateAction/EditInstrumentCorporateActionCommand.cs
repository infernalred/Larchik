using Larchik.Application.Models;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.EditInstrumentCorporateAction;

public record EditInstrumentCorporateActionCommand(Guid InstrumentId, Guid Id, InstrumentCorporateActionModel Model);

