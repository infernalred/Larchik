using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record CreatePortfolioCommand(PortfolioModel Model) : IRequest<Result<Guid>>;
