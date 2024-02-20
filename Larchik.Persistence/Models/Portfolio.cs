namespace Larchik.Persistence.Models;

public class Portfolio
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public decimal Income { get; set; }
    public decimal Outcome { get; set; }
    public decimal Dividends { get; set; }
    public decimal Coupons { get; set; }

    public Account? Account { get; set; }
    public AppUser? User { get; set; }
}