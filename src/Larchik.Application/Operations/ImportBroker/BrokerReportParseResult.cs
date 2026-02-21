using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.ImportBroker;

public record BrokerReportParseResult(
    List<ParsedOperation> Operations,
    List<string> Errors);

public record ParsedOperation(
    Operation Operation,
    string? InstrumentCode,
    bool RequiresInstrument);
