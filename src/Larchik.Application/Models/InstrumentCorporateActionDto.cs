using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public class InstrumentCorporateActionDto
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public OperationType Type { get; set; }
    public decimal Factor { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Note { get; set; } = null!;
}
