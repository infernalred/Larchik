using Larchik.Application.Helpers;
using Larchik.Domain.Enum;

namespace Larchik.Application.Deals;

public class DealParams : PagingParams
{
    public string? Ticker { get; set; }
    public DealKind? Type { get; set; }
}