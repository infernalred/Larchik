namespace Larchik.Application.Models;

public record StockModel(string Name, string Ticker, string Isin, int Kind, string CurrencyId, int CategoryId);