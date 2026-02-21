namespace Larchik.Infrastructure.Jobs;

public class JobExecutionResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static JobExecutionResult Success() => new() { IsSuccess = true };

    public static JobExecutionResult Failure(string error) => new()
    {
        IsSuccess = false,
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error
    };
}
