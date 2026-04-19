using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.ImportBroker;

internal static class BrokerImportBatchBuilder
{
    public static PreparedBrokerImportBatch Prepare(
        IReadOnlyCollection<ParsedOperation> parsedOperations,
        Guid portfolioId,
        BrokerImportInstrumentResolution resolution)
    {
        var importedKeys = new HashSet<string>(StringComparer.Ordinal);
        var baseKeyOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var preparedOperations = new List<Operation>(parsedOperations.Count);

        foreach (var parsed in parsedOperations)
        {
            if (parsed.RequiresInstrument)
            {
                parsed.Operation.InstrumentId = resolution.ResolveInstrumentId(parsed.InstrumentCode);
            }

            parsed.Operation.PortfolioId = portfolioId;

            var canonicalInstrumentCode = parsed.Operation.InstrumentId is { } instrumentId
                ? resolution.CanonicalInstrumentCodeById[instrumentId]
                : null;
            var baseKey = BrokerOperationKeyBuilder.BuildBaseHash(parsed.Operation, canonicalInstrumentCode);
            var occurrence = baseKeyOccurrences.GetValueOrDefault(baseKey) + 1;
            baseKeyOccurrences[baseKey] = occurrence;

            parsed.Operation.BrokerOperationKey =
                BrokerOperationKeyBuilder.Build(parsed.Operation, canonicalInstrumentCode, occurrence);

            importedKeys.Add(parsed.Operation.BrokerOperationKey);
            preparedOperations.Add(parsed.Operation);
        }

        return new PreparedBrokerImportBatch(preparedOperations, importedKeys);
    }
}

internal sealed record PreparedBrokerImportBatch(
    List<Operation> Operations,
    HashSet<string> ImportedKeys);
