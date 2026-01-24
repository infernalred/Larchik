using System;
using System.Threading;
using System.Threading.Tasks;

namespace Larchik.Application.Contracts;

public interface IPortfolioRecalcService
{
    Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default);
}
