using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using URLShortener.Web.Models;

namespace URLShortener.Web.Clients;

public sealed class UrlShortenerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public UrlShortenerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResponse<ShortenedUrlView>> ListLinksAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"api/links?page={page}&pageSize={pageSize}",
            cancellationToken);

        return await ReadRequiredAsync<PagedResponse<ShortenedUrlView>>(response, cancellationToken);
    }

    public async Task<ShortenedUrlView> CreateLinkAsync(
        CreateLinkRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/links",
            request,
            JsonOptions,
            cancellationToken);

        return await ReadRequiredAsync<ShortenedUrlView>(response, cancellationToken);
    }

    public async Task<bool> SetStatusAsync(
        string code,
        bool isActive,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/links/{Uri.EscapeDataString(code)}/status",
            new SetLinkStatusRequest(isActive),
            JsonOptions,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return true;
    }

    public async Task<LinkAnalyticsView?> GetAnalyticsAsync(
        string code,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var path = $"api/links/{Uri.EscapeDataString(code)}/analytics";
        var parameters = new List<string>();

        if (from is not null)
        {
            parameters.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to is not null)
        {
            parameters.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (parameters.Count > 0)
        {
            path += $"?{string.Join('&', parameters)}";
        }

        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadRequiredAsync<LinkAnalyticsView>(response, cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new ApiClientException(
                (int)response.StatusCode,
                "The API returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = $"The API returned HTTP {(int)response.StatusCode}.";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(
                JsonOptions,
                cancellationToken);

            var validationMessages = problem?.Errors?
                .SelectMany(item => item.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            message = validationMessages is { Length: > 0 }
                ? string.Join(" ", validationMessages)
                : problem?.Detail ?? problem?.Title ?? message;
        }
        catch (JsonException)
        {
            // Keep the status-based fallback when a non-ProblemDetails body is returned.
        }

        throw new ApiClientException((int)response.StatusCode, message);
    }
}
