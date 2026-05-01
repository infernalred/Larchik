using Larchik.Application.Models;

namespace Larchik.Application.Stocks.InstrumentCorporateActions.CreateInstrumentCorporateAction;

public record CreateInstrumentCorporateActionCommand(Guid InstrumentId, InstrumentCorporateActionModel Model);

