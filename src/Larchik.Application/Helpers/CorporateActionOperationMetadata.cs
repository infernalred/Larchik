namespace Larchik.Application.Helpers;

public static class CorporateActionOperationMetadata
{
    // Synthetic instrument corporate action operations are appended after same-day user operations.
    public static readonly DateTime SyntheticCreatedAt = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    public static readonly DateTime LegacySyntheticCreatedAt = SyntheticCreatedAt.AddSeconds(-1);
    private const string ContinuityNotePrefix = "System continuity:";

    public static bool IsSynthetic(DateTime createdAt) => createdAt == SyntheticCreatedAt;

    public static bool IsLegacyContinuityNote(string? note) =>
        !string.IsNullOrWhiteSpace(note) &&
        note.StartsWith(ContinuityNotePrefix, StringComparison.OrdinalIgnoreCase);
}
