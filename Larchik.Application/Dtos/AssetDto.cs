namespace Larchik.Application.Dtos;

public class AssetDto
{
    public Guid Id { get; set; }
    public StockDto Stock { get; set; }
    public decimal Quantity { get; set; }
}