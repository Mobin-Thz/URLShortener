using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using URLShortener.Web.Clients;
using URLShortener.Web.Models;
using URLShortener.Web.Pages;
using Xunit;

namespace URLShortener.Tests;

public sealed class IndexPageModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateHandler_WithValidForm_PostsTrimmedRequestAndRedirects()
    {
        var handler = new RecordingHandler(request =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(CreateLink("docs"))
            }));
        var model = CreateModel(handler);
        model.Form = new CreateLinkForm
        {
            OriginalUrl = "  https://example.com/docs  ",
            CustomAlias = "  docs  "
        };

        var result = await model.OnPostCreateAsync(cancellationToken: CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/links", handler.LastUri?.AbsolutePath);

        var request = JsonSerializer.Deserialize<CreateLinkRequest>(handler.LastBody!, JsonOptions);
        Assert.NotNull(request);
        Assert.Equal("https://example.com/docs", request.OriginalUrl);
        Assert.Equal("docs", request.CustomAlias);
        Assert.Contains("Created", model.SuccessMessage);
    }

    [Fact]
    public async Task CreateHandler_WithPastExpiration_DoesNotCallCreateEndpoint()
    {
        var handler = CreateListingHandler();
        var model = CreateModel(handler);
        model.Form = new CreateLinkForm
        {
            OriginalUrl = "https://example.com",
            ExpiresAtLocal = DateTime.Now.AddMinutes(-1)
        };

        var result = await model.OnPostCreateAsync(cancellationToken: CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Post);
        Assert.Contains(
            model.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage.Contains("future", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateHandler_WhenApiRejectsAlias_ShowsErrorAndReloadsLinks()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new
                    {
                        title = "Alias already exists",
                        detail = "The alias 'docs' is already in use."
                    })
                });
            }

            return Task.FromResult(ListResponse());
        });
        var model = CreateModel(handler);
        model.Form = new CreateLinkForm
        {
            OriginalUrl = "https://example.com",
            CustomAlias = "docs"
        };

        var result = await model.OnPostCreateAsync(cancellationToken: CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("already in use", model.ServiceError);
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task StatusHandler_PreservesPageNumberWhenRedirecting()
    {
        var handler = new RecordingHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var model = CreateModel(handler);

        var result = await model.OnPostStatusAsync(
            "docs",
            isActive: false,
            pageNumber: 3,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(3, redirect.RouteValues?["pageNumber"]);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal("/api/links/docs/status", handler.LastUri?.AbsolutePath);
    }

    [Fact]
    public void PageHandlers_DoNotUseReservedPageParameterName()
    {
        var handlerMethods = typeof(IndexModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name.StartsWith("On", StringComparison.Ordinal));

        Assert.DoesNotContain(
            handlerMethods.SelectMany(method => method.GetParameters()),
            parameter => string.Equals(parameter.Name, "page", StringComparison.OrdinalIgnoreCase));
    }

    private static IndexModel CreateModel(HttpMessageHandler handler) =>
        new(new UrlShortenerApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        }));

    private static RecordingHandler CreateListingHandler() =>
        new(_ => Task.FromResult(ListResponse()));

    private static HttpResponseMessage ListResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PagedResponse<ShortenedUrlView>(
                Array.Empty<ShortenedUrlView>(),
                1,
                10,
                0,
                0))
        };

    private static ShortenedUrlView CreateLink(string code) =>
        new(
            Guid.NewGuid(),
            "https://example.com/docs",
            $"http://api.test/{code}",
            code,
            DateTime.UtcNow,
            null,
            true,
            0,
            null);

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri? Uri)> Requests { get; } = new();
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUri = request.RequestUri;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri));
            return await responseFactory(request);
        }
    }
}
