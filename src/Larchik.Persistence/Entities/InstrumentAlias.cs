namespace Larchik.Persistence.Entities;

public class InstrumentAlias
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string AliasCode { get; set; } = null!;
    public string NormalizedAliasCode { get; set; } = null!;

    public Instrument? Instrument { get; set; }
}
