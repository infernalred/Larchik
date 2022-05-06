namespace Larchik.Application.Reports;

public class ReportResult
{
    public string FileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public byte[] FileData { get; set; } = null!;
}