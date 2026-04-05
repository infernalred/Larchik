namespace Larchik.Application.Models;

public record ImportResultDto(int ImportedOperations, int SkippedOperations, IReadOnlyCollection<string> Errors);
