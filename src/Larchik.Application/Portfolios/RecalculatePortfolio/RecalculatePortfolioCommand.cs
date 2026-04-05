using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.RecalculatePortfolio;

public record RecalculatePortfolioCommand(Guid Id) : IRequest<Result<RecalculatePortfolioResultDto>>;
