namespace Larchik.Application.Reports;

public class ReportResult
{
    public string FileName { get; set; }
    public string MimeType { get; set; }
    public byte[] FileData { get; set; }
}