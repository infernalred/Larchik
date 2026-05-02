using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

/// <summary>
/// Cheap snapshot of operations used to version portfolio summary cache keys after creates/edits/deletes.
/// Includes <see cref="MaxUpdatedTicks"/> so edits that bump <c>UpdatedAt</c> invalidate the cache without changing <c>CreatedAt</c>.
/// </summary>
public static class PortfolioSummaryOperationState
{
    public static async Task<(int Count, long MaxCreatedTicks, long MaxUpdatedTicks)> ForPortfolioAsync(
        LarchikContext context,
        Guid portfolioId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var count = await context.Operations
            .CountAsync(x => x.PortfolioId == portfolioId && x.TradeDate <= asOfUtc, cancellationToken);

        if (count == 0)
        {
            return (0, 0L, 0L);
        }

        var maxCreated = await context.Operations
            .Where(x => x.PortfolioId == portfolioId && x.TradeDate <= asOfUtc)
            .MaxAsync(x => x.CreatedAt, cancellationToken);

        var maxUpdated = await context.Operations
            .Where(x => x.PortfolioId == portfolioId && x.TradeDate <= asOfUtc)
            .MaxAsync(x => x.UpdatedAt, cancellationToken);

        return (count, maxCreated.Ticks, maxUpdated.Ticks);
    }

    public static async Task<(int Count, long MaxCreatedTicks, long MaxUpdatedTicks)> ForPortfoliosAsync(
        LarchikContext context,
        IReadOnlyCollection<Guid> portfolioIds,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        if (portfolioIds.Count == 0)
        {
            return (0, 0L, 0L);
        }

        var count = await context.Operations
            .CountAsync(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfUtc, cancellationToken);

        if (count == 0)
        {
            return (0, 0L, 0L);
        }

        var maxCreated = await context.Operations
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfUtc)
            .MaxAsync(x => x.CreatedAt, cancellationToken);

        var maxUpdated = await context.Operations
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfUtc)
            .MaxAsync(x => x.UpdatedAt, cancellationToken);

        return (count, maxCreated.Ticks, maxUpdated.Ticks);
    }
}
