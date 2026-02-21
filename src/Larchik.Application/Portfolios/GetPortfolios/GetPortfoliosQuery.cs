using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.GetPortfolios;

public record GetPortfoliosQuery : IRequest<Result<IReadOnlyCollection<PortfolioDto>>>;
