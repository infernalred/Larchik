namespace Larchik.Application.Operations.ImportBroker;

public record ImportBrokerReportCommand(
    Guid PortfolioId,
    string BrokerCode,
    Stream FileStream,
    string FileName,
    bool StrictUnknownCashMapping = false);

