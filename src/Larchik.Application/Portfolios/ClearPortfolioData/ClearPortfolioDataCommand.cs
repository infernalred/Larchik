using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.ClearPortfolioData;

public record ClearPortfolioDataCommand(Guid Id) : IRequest<Result<ClearPortfolioDataResultDto>>;
