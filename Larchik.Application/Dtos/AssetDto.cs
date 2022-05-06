namespace Larchik.Application.Dtos;

public class AssetDto
{
    public Guid Id { get; set; }
    public StockDto Stock { get; set; } = null!;
    public decimal Quantity { get; set; }
}