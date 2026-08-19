using System.Net;
using System.Net.Http.Json;
using URLShortener.Web.Clients;
using URLShortener.Web.Models;
using Xunit;

namespace URLShortener.Tests;

public sealed class UrlShortenerApiClientTests
{
    [Fact]
    public async Task ListLinksAsync_DeserializesApiContract()
    {
        var link = new ShortenedUrlView(
            Guid.NewGuid(),
            "https://example.com/docs",
            "https://sho.rt/docs",
            "docs",
            DateTime.UtcNow,
            null,
            true,
            12,
            DateTime.UtcNow);

        using var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PagedResponse<ShortenedUrlView>(
                new[] { link },
                1,
                10,
                1,
                1))
        });
        var client = new UrlShortenerApiClient(httpClient);

        var result = await client.ListLinksAsync(1, 10, CancellationToken.None);

        var returnedLink = Assert.Single(result.Items);
        Assert.Equal("docs", returnedLink.Code);
        Assert.Equal(12, returnedLink.TotalClicks);
    }

    [Fact]
    public async Task CreateLinkAsync_ConvertsProblemDetailsIntoUsefulException()
    {
        using var httpClient = CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new
            {
                title = "Alias already exists",
                detail = "The alias 'docs' is already in use."
            })
        });
        var client = new UrlShortenerApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ApiClientException>(() => client.CreateLinkAsync(
            new CreateLinkRequest("https://example.com", "docs", null),
            CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("already in use", exception.Message);
    }

    [Fact]
    public async Task SetStatusAsync_WhenLinkDoesNotExist_ReturnsFalse()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = CreateHttpClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var client = new UrlShortenerApiClient(httpClient);

        var result = await client.SetStatusAsync("missing link", false, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(HttpMethod.Put, capturedRequest?.Method);
        Assert.Equal("/api/links/missing%20link/status", capturedRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WhenLinkDoesNotExist_ReturnsNull()
    {
        using var httpClient = CreateHttpClient(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new UrlShortenerApiClient(httpClient);

        var result = await client.GetAnalyticsAsync(
            "missing",
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Null(result);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHandler(responseFactory))
        {
            BaseAddress = new Uri("http://api.test/")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
