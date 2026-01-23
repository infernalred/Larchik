using System;

namespace Larchik.Application.Models;

public class RealizedPnlDto
{
    public Guid InstrumentId { get; set; }
    public string InstrumentName { get; set; } = null!;
    public string CurrencyId { get; set; } = null!;
    public decimal Realized { get; set; }
    public decimal RealizedBase { get; set; }
}
