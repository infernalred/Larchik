using Larchik.Application.Helpers;
using Larchik.Persistence.Enum;

namespace Larchik.Application.Deals;

public class DealParams : PageFilter
{
    public string? Ticker { get; set; }
    public OperationKind? Type { get; set; }
}