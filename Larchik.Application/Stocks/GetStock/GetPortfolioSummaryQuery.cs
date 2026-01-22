using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

public record GetPortfolioSummaryQuery(Guid Id) : IRequest<Result<PortfolioSummaryDto>>;
