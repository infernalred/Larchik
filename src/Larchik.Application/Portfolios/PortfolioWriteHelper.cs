using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

internal static class PortfolioWriteHelper
{
    public static async Task<Result<ResolvedPortfolioInput>> ResolveInputAsync(
        LarchikContext context,
        PortfolioModel model,
        CancellationToken cancellationToken)
    {
        if (model.BrokerId == Guid.Empty)
        {
            return Result<ResolvedPortfolioInput>.Failure("Выберите брокера.");
        }

        var brokerExists = await context.Brokers
            .AsNoTracking()
            .AnyAsync(x => x.Id == model.BrokerId, cancellationToken);
        if (!brokerExists)
        {
            return Result<ResolvedPortfolioInput>.Failure("Selected broker was not found.");
        }

        var reportingCurrencyId = PortfolioInputNormalizer.NormalizeCurrencyId(model.ReportingCurrencyId);
        if (reportingCurrencyId is null)
        {
            return Result<ResolvedPortfolioInput>.Failure("Reporting currency must be a 3-letter code.");
        }

        var currencyExists = await context.Currencies
            .AsNoTracking()
            .AnyAsync(x => x.Id == reportingCurrencyId, cancellationToken);
        if (!currencyExists)
        {
            return Result<ResolvedPortfolioInput>.Failure("Selected reporting currency was not found.");
        }

        return Result<ResolvedPortfolioInput>.Success(new ResolvedPortfolioInput(
            PortfolioInputNormalizer.NormalizeName(model.Name),
            model.BrokerId,
            reportingCurrencyId));
    }

    internal sealed record ResolvedPortfolioInput(
        string Name,
        Guid BrokerId,
        string ReportingCurrencyId);
}
