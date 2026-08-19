using URLShortener.Application.DTOs;
using URLShortener.Application.Models;

namespace URLShortener.Application.Services;

public interface IUrlShortenerService
{
    Task<ShortenedUrlResponse> CreateAsync(
        ShortenUrlRequest request,
        string baseUrl,
        CancellationToken cancellationToken);

    Task<ShortenedUrlResponse?> GetAsync(
        string code,
        string baseUrl,
        CancellationToken cancellationToken);

    Task<PagedResponse<ShortenedUrlResponse>> ListAsync(
        int page,
        int pageSize,
        string baseUrl,
        CancellationToken cancellationToken);

    Task<LinkResolution> ResolveAsync(
        string code,
        ClickContext clickContext,
        CancellationToken cancellationToken);

    Task<LinkAnalyticsResponse?> GetAnalyticsAsync(
        string code,
        DateTime from,
        DateTime to,
        string baseUrl,
        CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(
        string code,
        bool isActive,
        CancellationToken cancellationToken);
}
