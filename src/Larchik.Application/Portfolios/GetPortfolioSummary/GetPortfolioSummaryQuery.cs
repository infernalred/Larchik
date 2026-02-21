using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

public record GetPortfolioSummaryQuery(Guid Id, string? Method = null) : IRequest<Result<PortfolioSummaryDto>>;
