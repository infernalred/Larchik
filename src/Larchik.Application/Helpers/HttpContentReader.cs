using System.Text;

namespace Larchik.Application.Helpers;

public static class HttpContentReader
{
    public static async Task<string> ReadAsStringSafeAsync(HttpContent content, CancellationToken cancellationToken = default)
    {
        try
        {
            return await content.ReadAsStringAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await ReadWithFallbackEncoding(content, cancellationToken);
        }
        catch (ArgumentException)
        {
            return await ReadWithFallbackEncoding(content, cancellationToken);
        }
    }

    private static async Task<string> ReadWithFallbackEncoding(HttpContent content, CancellationToken cancellationToken)
    {
        var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
