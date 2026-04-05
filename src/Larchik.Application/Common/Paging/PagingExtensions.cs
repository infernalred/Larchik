using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Common.Paging;

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PageQuery? paging,
        int maxPageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var pageQuery = paging ?? new PageQuery();
        var (page, pageSize) = pageQuery.Normalize(maxPageSize);

        var totalCount = await source.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new PagedResult<T>
            {
                Items = [],
                Page = PageQuery.DefaultPage,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var effectivePage = Math.Min(page, totalPages);

        var items = await source
            .Skip((effectivePage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            Page = effectivePage,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
