using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfoliosSummary;

public record GetPortfoliosSummaryQuery(string? Method = null, string? Currency = null)
    : IRequest<Result<PortfoliosSummaryDto>>;
