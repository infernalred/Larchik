namespace Larchik.API.Configuration;

public class ImportOptions
{
    public const string SectionName = "Import";
    public int MaxFileSizeMb { get; set; } = 10;

    public long MaxFileSizeBytes => Math.Max(MaxFileSizeMb, 1) * 1024L * 1024L;
}
