namespace Larchik.Application.MarketDataImports.Processing;

public sealed class MarketDataImportOptions
{
    public const string SectionName = "MarketDataImports";

    public int ChunkDays { get; set; } = 90;
    public int MaxAttempts { get; set; } = 5;
    public int DefaultCategoryId { get; set; } = 14;
    public int EtfCategoryId { get; set; } = 22;
}
