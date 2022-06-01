namespace Larchik.Application.Dtos;

public class DealDto
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Stock { get; set; } = string.Empty;
    public double Commission { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid AccountId { get; set; }
}