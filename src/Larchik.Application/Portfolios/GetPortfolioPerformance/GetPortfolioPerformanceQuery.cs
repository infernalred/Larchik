using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfolioPerformance;

public record GetPortfolioPerformanceQuery(
    Guid Id,
    string? Method = null,
    DateTime? From = null,
    DateTime? To = null) : IRequest<Result<IReadOnlyCollection<PortfolioPerformanceDto>>>;
