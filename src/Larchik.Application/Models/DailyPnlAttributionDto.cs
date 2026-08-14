namespace Larchik.Application.Models;

public sealed record DailyPnlAttributionDto
{
    public Guid? PortfolioId { get; init; }
    public string Name { get; init; } = null!;
    public string ReportingCurrencyId { get; init; } = null!;
    public DateTime ComparisonDate { get; init; }
    public DateTime ValuationDate { get; init; }
    public decimal StartNavBase { get; init; }
    public decimal EndNavBase { get; init; }
    public decimal ExternalFlowBase { get; init; }
    public decimal PnlBase { get; init; }
    public decimal? ReturnPct { get; init; }
    public decimal PriceEffectBase { get; init; }
    public decimal SecurityFxEffectBase { get; init; }
    public decimal CrossEffectBase { get; init; }
    public decimal TradingEffectBase { get; init; }
    public decimal CashFxEffectBase { get; init; }
    public decimal IncomeEffectBase { get; init; }
    public decimal FeeEffectBase { get; init; }
    public decimal OtherEffectBase { get; init; }
    public decimal FxEffectBase => SecurityFxEffectBase + CashFxEffectBase;
    public decimal ReconciliationResidualBase { get; init; }
    public bool IsComplete { get; init; }
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public IReadOnlyCollection<PositionDailyPnlAttributionDto> Positions { get; init; } = [];
    public IReadOnlyCollection<CashDailyPnlAttributionDto> Cash { get; init; } = [];
}

public sealed record PositionDailyPnlAttributionDto
{
    public Guid InstrumentId { get; init; }
    public string InstrumentName { get; init; } = null!;
    public string? InstrumentType { get; init; }
    public string? CategoryName { get; init; }
    public string CurrencyId { get; init; } = null!;
    public decimal StartQuantity { get; init; }
    public decimal EndQuantity { get; init; }
    public decimal? StartPrice { get; init; }
    public decimal? EndPrice { get; init; }
    public DateTime? StartPriceDate { get; init; }
    public DateTime? EndPriceDate { get; init; }
    public decimal? StartFxRate { get; init; }
    public decimal? EndFxRate { get; init; }
    public DateTime? StartFxRateDate { get; init; }
    public DateTime? EndFxRateDate { get; init; }
    public decimal StartMarketValueBase { get; init; }
    public decimal EndMarketValueBase { get; init; }
    public decimal PnlBase { get; init; }
    public decimal? ReturnPct { get; init; }
    public decimal? PriceReturnPct { get; init; }
    public decimal? FxReturnPct { get; init; }
    public decimal? TotalMarketReturnPct { get; init; }
    public decimal PriceEffectBase { get; init; }
    public decimal FxEffectBase { get; init; }
    public decimal CrossEffectBase { get; init; }
    public decimal TradingEffectBase { get; init; }
    public decimal IncomeEffectBase { get; init; }
    public decimal FeeEffectBase { get; init; }
    public decimal OtherEffectBase { get; init; }
    public string DataQuality { get; init; } = "complete";
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
}

public sealed record CashDailyPnlAttributionDto
{
    public string CurrencyId { get; init; } = null!;
    public decimal StartAmount { get; init; }
    public decimal EndAmount { get; init; }
    public decimal? StartFxRate { get; init; }
    public decimal? EndFxRate { get; init; }
    public decimal FxEffectBase { get; init; }
    public string DataQuality { get; init; } = "complete";
}
