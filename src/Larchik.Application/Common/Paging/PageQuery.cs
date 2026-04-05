namespace Larchik.Application.Common.Paging;

public class PageQuery
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 50;

    public int Page { get; set; } = DefaultPage;
    public int PageSize { get; set; } = DefaultPageSize;

    public (int Page, int PageSize) Normalize(int maxPageSize = 200)
    {
        var page = Page > 0 ? Page : DefaultPage;
        var pageSize = PageSize switch
        {
            <= 0 => DefaultPageSize,
            > 0 when maxPageSize > 0 && PageSize > maxPageSize => maxPageSize,
            _ => PageSize
        };

        return (page, pageSize);
    }
}
