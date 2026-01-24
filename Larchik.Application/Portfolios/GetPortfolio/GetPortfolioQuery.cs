using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfolio;

public record GetPortfolioQuery(Guid Id) : IRequest<Result<PortfolioDto?>>;
