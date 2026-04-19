using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

internal static class PortfolioQueryHelper
{
    public static IQueryable<PortfolioDto> ProjectToDto(this IQueryable<Portfolio> query) =>
        query.Select(x => new PortfolioDto
        {
            Id = x.Id,
            Name = x.Name,
            BrokerId = x.BrokerId,
            ReportingCurrencyId = x.ReportingCurrencyId,
            CreatedAt = x.CreatedAt
        });
}
