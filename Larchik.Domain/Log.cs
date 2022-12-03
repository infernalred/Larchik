namespace Larchik.Domain;

public class Log
{
    public long Id { get; set; }
    public string MachineName { get; set; } = null!;
    public DateTime Logged { get; set; }
    public string Level { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Logger { get; set; }
    public string? CallSite { get; set; }
    public string? Exception { get; set; }
}