using System.Net.Http;
using System.Text;
using Larchik.Application.Helpers;
using Xunit;

namespace Larchik.Application.Tests.Helpers;

public class HttpContentReaderTests
{
    [Fact]
    public async Task ReadAsStringSafeAsync_FallsBackToUtf8_WhenCharsetIsInvalid()
    {
        var payload = "{\"message\":\"тест\"}";
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=broken-charset");

        var actual = await HttpContentReader.ReadAsStringSafeAsync(content);

        Assert.Equal(payload, actual);
    }
}
