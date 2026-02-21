namespace Larchik.Application.Models;

public class CashBalanceDto
{
    public string CurrencyId { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal AmountInBase { get; set; }
}
