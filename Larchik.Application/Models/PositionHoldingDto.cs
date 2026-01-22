using System;

namespace Larchik.Application.Models;

public class PositionHoldingDto
{
    public Guid InstrumentId { get; set; }
    public string InstrumentName { get; set; } = null!;
    public string CurrencyId { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? LastPrice { get; set; }
    public decimal MarketValueBase { get; set; }
    public decimal AverageCost { get; set; }
}
