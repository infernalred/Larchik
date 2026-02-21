namespace Larchik.Application.Helpers;

public class AppException(int statusCode, string message, string? details = null)
{
    public int StatusCode { get; init; } = statusCode;
    public string Message { get; init; } = message;
    public string? Details { get; init; } = details;
}