using System.Net;

namespace Larchik.Application.Tests.TestDoubles;

internal sealed class FakeHttpClientFactory(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new FakeHttpMessageHandler(responder))
    {
        BaseAddress = new Uri("https://test.local")
    };

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
