using Larchik.Application.Helpers;

namespace Larchik.Application.Deals;

public class DealParams : PagingParams
{
    public string? Ticker { get; set; }
    public string? Operation { get; set; }
}