using Larchik.Domain.Enum;
using Larchik.Persistence.Enum;

namespace Larchik.Application.Dtos;

public class DealDto
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OperationKind Type { get; set; }
    public string Currency { get; set; } = null!;
    public string? Stock { get; set; }
    public decimal Commission { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid AccountId { get; set; }
}