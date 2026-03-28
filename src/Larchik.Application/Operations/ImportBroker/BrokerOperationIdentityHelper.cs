using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.ImportBroker;

internal static class BrokerOperationIdentityHelper
{
    private const string ConfirmedPrefix = "v2:";
    private const string ProvisionalManualPrefix = "manual:v2:";

    public static bool SupportsProvisionalManualKeys(string? brokerCode) =>
        string.Equals(brokerCode, "tbank", StringComparison.OrdinalIgnoreCase);

    public static bool IsConfirmedImportedKey(string? brokerOperationKey) =>
        !string.IsNullOrWhiteSpace(brokerOperationKey) &&
        brokerOperationKey.StartsWith(ConfirmedPrefix, StringComparison.Ordinal);

    public static bool IsProvisionalManualKey(string? brokerOperationKey) =>
        !string.IsNullOrWhiteSpace(brokerOperationKey) &&
        brokerOperationKey.StartsWith(ProvisionalManualPrefix, StringComparison.Ordinal);

    public static bool IsManualCandidateKey(string? brokerOperationKey) =>
        string.IsNullOrWhiteSpace(brokerOperationKey) || IsProvisionalManualKey(brokerOperationKey);

    public static async Task<string?> BuildProvisionalManualKeyAsync(
        LarchikContext context,
        Guid portfolioId,
        string? brokerCode,
        Operation operation,
        string? canonicalInstrumentCode,
        Guid? excludeOperationId,
        CancellationToken cancellationToken)
    {
        if (!SupportsProvisionalManualKeys(brokerCode))
        {
            return null;
        }

        var baseHash = BrokerOperationKeyBuilder.BuildBaseHash(operation, canonicalInstrumentCode);
        var confirmedKeyPrefix = $"{ConfirmedPrefix}{baseHash}:";
        var provisionalKeyPrefix = $"{ProvisionalManualPrefix}{baseHash}:";

        var existingKeys = await context.Operations
            .AsNoTracking()
            .Where(x =>
                x.PortfolioId == portfolioId &&
                x.BrokerOperationKey != null &&
                (x.BrokerOperationKey.StartsWith(confirmedKeyPrefix) || x.BrokerOperationKey.StartsWith(provisionalKeyPrefix)) &&
                (!excludeOperationId.HasValue || x.Id != excludeOperationId.Value))
            .Select(x => x.BrokerOperationKey!)
            .ToArrayAsync(cancellationToken);

        var nextOccurrence = existingKeys
            .Select(TryParseOccurrence)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{ProvisionalManualPrefix}{baseHash}:{nextOccurrence:D6}";
    }

    private static int? TryParseOccurrence(string brokerOperationKey)
    {
        var lastColonIndex = brokerOperationKey.LastIndexOf(':');
        if (lastColonIndex < 0 || lastColonIndex == brokerOperationKey.Length - 1)
        {
            return null;
        }

        return int.TryParse(brokerOperationKey[(lastColonIndex + 1)..], out var occurrence)
            ? occurrence
            : null;
    }
}
