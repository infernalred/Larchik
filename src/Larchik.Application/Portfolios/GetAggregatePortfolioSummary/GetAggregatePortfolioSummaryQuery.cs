using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioSummary;

public record GetAggregatePortfolioSummaryQuery(string? Method = null, string? Currency = null)
    : IRequest<Result<PortfolioSummaryDto>>;
