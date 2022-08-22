using Larchik.Application.Portfolios;

namespace Larchik.Application.Services.Contracts;

public interface IPortfolioService
{
    Task<Portfolio> GetPortfolioAsync(CancellationToken cancellationToken);
    Task<Portfolio> GetPortfolioAsync(Guid id, CancellationToken cancellationToken);
}