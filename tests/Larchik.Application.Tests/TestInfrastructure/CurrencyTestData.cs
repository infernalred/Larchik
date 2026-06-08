using Larchik.Persistence.Entities;

namespace Larchik.Application.Tests.TestInfrastructure;

public static class CurrencyTestData
{
    public static readonly Currency Rub = Create("RUB", "Российский рубль");
    public static readonly Currency Usd = Create("USD", "Доллар США");
    public static readonly Currency Eur = Create("EUR", "Евро");

    public static Currency Create(string id, string? name = null) =>
        new()
        {
            Id = id,
            Name = name ?? id
        };
}
