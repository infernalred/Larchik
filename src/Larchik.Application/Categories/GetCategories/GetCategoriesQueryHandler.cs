using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Categories.GetCategories;

public class GetCategoriesQueryHandler(LarchikContext context) : IRequestHandler<GetCategoriesQuery, Result<Category[]>>
{
    public async Task<Result<Category[]>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Categories
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);

        return Result<Category[]>.Success(result);
    }
}