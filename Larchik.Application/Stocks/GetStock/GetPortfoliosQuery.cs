using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record GetPortfoliosQuery : IRequest<Result<IReadOnlyCollection<PortfolioDto>>>;
