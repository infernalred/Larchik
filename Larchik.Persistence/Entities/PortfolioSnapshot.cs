namespace Larchik.Persistence.Entities;

public class PortfolioSnapshot
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public DateTime Date { get; set; }
    public decimal NavBase { get; set; }
    public decimal PnlDayBase { get; set; }
    public decimal PnlMonthBase { get; set; }
    public decimal PnlYearBase { get; set; }
    public decimal CashBase { get; set; }

    public Portfolio? Portfolio { get; set; }
}
