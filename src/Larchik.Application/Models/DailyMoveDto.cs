namespace Larchik.Application.Models;

public class DailyMoveDto
{
    public decimal StartValueBase { get; set; }
    public decimal PnlBase { get; set; }
    public decimal? ReturnPct { get; set; }
    public decimal PriceEffectBase { get; set; }
    public decimal FxEffectBase { get; set; }
    public decimal CrossEffectBase { get; set; }
    public decimal TradingEffectBase { get; set; }
    public decimal IncomeEffectBase { get; set; }
    public decimal FeeEffectBase { get; set; }
    public decimal OtherEffectBase { get; set; }
    public string DataQuality { get; set; } = "complete";
}

public sealed class PortfolioDailyMoveDto : DailyMoveDto
{
    public DateTime ComparisonDate { get; set; }
    public DateTime ValuationDate { get; set; }
}
