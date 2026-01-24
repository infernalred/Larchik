using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record EditPortfolioCommand(Guid Id, PortfolioModel Model) : IRequest<Result<Unit>>;
