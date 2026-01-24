using System;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public class OperationDto
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid? InstrumentId { get; set; }
    public OperationType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string CurrencyId { get; set; } = null!;
    public DateTime TradeDate { get; set; }
    public DateTime? SettlementDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
