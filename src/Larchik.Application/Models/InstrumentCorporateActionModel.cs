using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public record InstrumentCorporateActionModel(
    OperationType Type,
    decimal Factor,
    DateTimeOffset EffectiveDate,
    string Note);
