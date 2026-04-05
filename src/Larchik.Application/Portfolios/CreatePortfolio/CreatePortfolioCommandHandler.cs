using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.CreatePortfolio;

public class CreatePortfolioCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<CreatePortfolioCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var brokerId = request.Model.BrokerId;
        var trimmedName = request.Model.Name.Trim();

        if (brokerId == Guid.Empty)
        {
            return Result<Guid>.Failure("Выберите брокера.");
        }

        var brokerExists = await context.Brokers
            .AnyAsync(x => x.Id == brokerId, cancellationToken);
        if (!brokerExists)
        {
            return Result<Guid>.Failure("Выбранный брокер не найден.");
        }

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            BrokerId = brokerId,
            ReportingCurrencyId = request.Model.ReportingCurrencyId.ToUpperInvariant(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(portfolio.Id);
    }
}
