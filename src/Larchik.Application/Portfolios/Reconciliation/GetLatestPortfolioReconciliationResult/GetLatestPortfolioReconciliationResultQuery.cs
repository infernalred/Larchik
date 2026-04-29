using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.Reconciliation.GetLatestPortfolioReconciliationResult;

public sealed record GetLatestPortfolioReconciliationResultQuery(
    Guid PortfolioId,
    string? Source = null) : IRequest<Result<PortfolioReconciliationResultDto>>;
