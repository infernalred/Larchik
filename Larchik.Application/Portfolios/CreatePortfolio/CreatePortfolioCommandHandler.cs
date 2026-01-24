using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;

namespace Larchik.Application.Portfolios.CreatePortfolio;

public class CreatePortfolioCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreatePortfolioCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = request.Model.Name,
            BrokerId = request.Model.BrokerId,
            ReportingCurrencyId = request.Model.ReportingCurrencyId.ToUpperInvariant(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(portfolio.Id);
    }
}
