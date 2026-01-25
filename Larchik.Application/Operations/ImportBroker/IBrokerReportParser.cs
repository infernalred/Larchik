namespace Larchik.Application.Operations.ImportBroker;

public interface IBrokerReportParser
{
    string Code { get; }
    Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
}
