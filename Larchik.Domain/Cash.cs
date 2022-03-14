namespace Larchik.Domain;

public class Cash
{
    public Guid Id { get; set; }
    public Account Account { get; set; }
    public Guid AccountId { get; set; }
    public Currency Currency { get; set; }
    public string CurrencyId { get; set; }
    public decimal Amount { get; set; }
}