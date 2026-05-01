namespace Larchik.Application.Stocks.SearchInstruments;

public record SearchInstrumentsQuery(string? Query, int Limit = 20);
