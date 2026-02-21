namespace Larchik.Application.Models;

public class PortfolioDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid BrokerId { get; set; }
    public string ReportingCurrencyId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
