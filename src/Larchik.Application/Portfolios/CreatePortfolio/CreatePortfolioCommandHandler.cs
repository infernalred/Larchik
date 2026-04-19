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
        var resolvedInputResult = await PortfolioWriteHelper.ResolveInputAsync(context, request.Model, cancellationToken);
        if (!resolvedInputResult.IsSuccess)
        {
            return Result<Guid>.Failure(resolvedInputResult.Error!);
        }

        var resolvedInput = resolvedInputResult.Value!;
        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            Name = resolvedInput.Name,
            BrokerId = resolvedInput.BrokerId,
            ReportingCurrencyId = resolvedInput.ReportingCurrencyId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(portfolio.Id);
    }
}
