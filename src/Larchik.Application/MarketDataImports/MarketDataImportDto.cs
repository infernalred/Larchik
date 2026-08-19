using Larchik.Persistence.Entities;

namespace Larchik.Application.MarketDataImports;

public record MarketDataImportDto(
    Guid Id,
    PriceSource Source,
    string Isin,
    DateOnly FromDate,
    DateOnly ToDate,
    DateOnly NextDate,
    MarketDataImportStatus Status,
    Guid? InstrumentId,
    int InsertedPrices,
    int UpdatedPrices,
    int Attempt,
    string? LastError,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt)
{
    public static MarketDataImportDto FromEntity(MarketDataImportRequest request) =>
        new(
            request.Id,
            request.Source,
            request.Isin,
            DateOnly.FromDateTime(request.FromDate),
            DateOnly.FromDateTime(request.ToDate),
            DateOnly.FromDateTime(request.NextDate),
            request.Status,
            request.InstrumentId,
            request.InsertedPrices,
            request.UpdatedPrices,
            request.Attempt,
            request.LastError,
            request.CreatedAt,
            request.StartedAt,
            request.CompletedAt);
}
