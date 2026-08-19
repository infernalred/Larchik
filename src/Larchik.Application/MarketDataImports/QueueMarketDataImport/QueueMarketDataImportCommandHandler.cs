using System.Text.Json;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.MarketDataImports.QueueMarketDataImport;

public sealed class QueueMarketDataImportCommandHandler(LarchikContext context, IUserAccessor userAccessor)
{
    public async Task<Result<MarketDataImportDto>> Handle(
        QueueMarketDataImportCommand command,
        CancellationToken cancellationToken)
    {
        var isin = command.Isin.Trim().ToUpperInvariant();
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? null
            : command.IdempotencyKey.Trim();

        if (idempotencyKey is not null)
        {
            var existingRequest = await context.MarketDataImportRequests
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existingRequest is not null)
            {
                return Result<MarketDataImportDto>.Success(MarketDataImportDto.FromEntity(existingRequest));
            }
        }

        var now = DateTime.UtcNow;
        var fromDate = ToUtcDate(command.FromDate);
        var toDate = ToUtcDate(DateOnly.FromDateTime(now));
        var existingInstrument = await context.Instruments
            .FirstOrDefaultAsync(x => x.Isin != null && x.Isin.ToUpper() == isin, cancellationToken);
        var request = new MarketDataImportRequest
        {
            Id = Guid.NewGuid(),
            RequestedBy = userAccessor.GetUserId(),
            Source = command.Source,
            Isin = isin,
            FromDate = fromDate,
            ToDate = toDate,
            NextDate = fromDate,
            Status = existingInstrument is null
                ? MarketDataImportStatus.Queued
                : MarketDataImportStatus.SkippedExisting,
            InstrumentId = existingInstrument?.Id,
            IdempotencyKey = idempotencyKey,
            CompletedAt = existingInstrument is null ? null : now,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.MarketDataImportRequests.Add(request);

        if (existingInstrument is null)
        {
            var message = MarketDataImportMessage.Create(request.Id);
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MarketDataImportMessage.MessageType,
                PayloadJson = JsonSerializer.Serialize(message),
                OccurredAt = now,
                AvailableAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result<MarketDataImportDto>.Success(MarketDataImportDto.FromEntity(request));
    }

    private static DateTime ToUtcDate(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}
