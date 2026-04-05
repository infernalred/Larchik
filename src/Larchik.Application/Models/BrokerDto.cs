namespace Larchik.Application.Models;

public record BrokerDto(Guid Id, string? Code, string Name, string? Country, bool SupportsImport);
