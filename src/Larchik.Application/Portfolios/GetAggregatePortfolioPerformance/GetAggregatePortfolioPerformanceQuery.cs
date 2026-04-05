using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;

public record GetAggregatePortfolioPerformanceQuery(
    string? Method = null,
    string? Currency = null,
    DateTime? From = null,
    DateTime? To = null)
    : IRequest<Result<IReadOnlyCollection<PortfolioPerformanceDto>>>;
