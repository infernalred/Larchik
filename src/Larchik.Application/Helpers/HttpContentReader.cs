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
        catch (NotSupportedException)
        {
            return await ReadWithFallbackEncoding(content, cancellationToken);
        }
    }

    private static async Task<string> ReadWithFallbackEncoding(HttpContent content, CancellationToken cancellationToken)
    {
        var bytes = await content.ReadAsByteArrayAsync(cancellationToken);

        foreach (var encoding in GetFallbackEncodings(content))
        {
            try
            {
                return encoding.GetString(bytes);
            }
            catch (ArgumentException)
            {
                // Try next fallback.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static IEnumerable<Encoding> GetFallbackEncodings(HttpContent content)
    {
        var charSet = content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            Encoding? declaredEncoding = null;
            try
            {
                declaredEncoding = Encoding.GetEncoding(charSet.Trim().Trim('"'));
            }
            catch (ArgumentException)
            {
                declaredEncoding = null;
            }

            if (declaredEncoding is not null)
            {
                yield return declaredEncoding;
            }
        }

        yield return Encoding.UTF8;
        yield return Encoding.GetEncoding(1251);
    }
}
