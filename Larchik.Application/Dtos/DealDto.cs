namespace Larchik.Application.Dtos;

public class DealDto
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Operation { get; set; }
    public string Stock { get; set; }
    public decimal Commission { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid AccountId { get; set; }
}