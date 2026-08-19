using System.Security.Cryptography;
using System.Text;
using URLShortener.Application.DTOs;
using URLShortener.Application.Exceptions;
using URLShortener.Application.Interfaces;
using URLShortener.Application.Models;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services;

public sealed class UrlShortenerService : IUrlShortenerService
{
    private const string CodeCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int GeneratedCodeLength = 7;
    private readonly IUrlShortenerRepository _repository;

    public UrlShortenerService(IUrlShortenerRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShortenedUrlResponse> CreateAsync(
        ShortenUrlRequest request,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var customAlias = request.CustomAlias?.Trim();
        if (!string.IsNullOrEmpty(customAlias))
        {
            var customLink = CreateEntity(request, customAlias, baseUrl);
            if (!await _repository.TryAddAsync(customLink, cancellationToken))
            {
                throw new AliasAlreadyExistsException(customAlias);
            }

            return Map(customLink, baseUrl);
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var link = CreateEntity(request, GenerateCode(), baseUrl);
            if (await _repository.TryAddAsync(link, cancellationToken))
            {
                return Map(link, baseUrl);
            }
        }

        throw new InvalidOperationException("Could not generate a unique short code after multiple attempts.");
    }

    private static ShortenedUrl CreateEntity(ShortenUrlRequest request, string code, string baseUrl) =>
        new()
        {
            Id = Guid.NewGuid(),
            OriginalUrl = request.OriginalUrl.Trim(),
            ShortUrl = BuildShortUrl(baseUrl, code),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = NormalizeUtc(request.ExpiresAt),
            IsActive = true
        };

    public async Task<ShortenedUrlResponse?> GetAsync(
        string code,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var shortenedUrl = await _repository.FindByCodeAsync(code, cancellationToken);
        return shortenedUrl is null ? null : Map(shortenedUrl, baseUrl);
    }

    public async Task<PagedResponse<ShortenedUrlResponse>> ListAsync(
        int page,
        int pageSize,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var items = await _repository.ListAsync((page - 1) * pageSize, pageSize, cancellationToken);
        var count = await _repository.CountAsync(cancellationToken);

        return new PagedResponse<ShortenedUrlResponse>(
            items.Select(item => Map(item, baseUrl)).ToArray(),
            page,
            pageSize,
            count);
    }

    public async Task<LinkResolution> ResolveAsync(
        string code,
        ClickContext clickContext,
        CancellationToken cancellationToken)
    {
        var shortenedUrl = await _repository.FindByCodeAsync(code, cancellationToken);
        if (shortenedUrl is null)
        {
            return new LinkResolution(LinkResolutionStatus.NotFound);
        }

        var clickedAt = DateTime.UtcNow;
        if (!shortenedUrl.CanRedirectAt(clickedAt))
        {
            return new LinkResolution(LinkResolutionStatus.Unavailable);
        }

        var clickEvent = new ClickEvent
        {
            ShortenedUrlId = shortenedUrl.Id,
            ClickedAt = clickedAt,
            VisitorHash = CreateVisitorHash(clickContext, clickedAt),
            ReferrerHost = GetReferrerHost(clickContext.Referrer),
            Browser = DetectBrowser(clickContext.UserAgent),
            DeviceType = DetectDevice(clickContext.UserAgent),
            CountryCode = NormalizeCountryCode(clickContext.CountryCode)
        };

        await _repository.RecordClickAsync(shortenedUrl, clickEvent, cancellationToken);
        return new LinkResolution(LinkResolutionStatus.Found, shortenedUrl.OriginalUrl);
    }

    public async Task<LinkAnalyticsResponse?> GetAnalyticsAsync(
        string code,
        DateTime from,
        DateTime to,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var data = await _repository.GetAnalyticsAsync(code, from, to, cancellationToken);
        if (data is null)
        {
            return null;
        }

        return new LinkAnalyticsResponse(
            data.Code,
            BuildShortUrl(baseUrl, data.Code),
            from,
            to,
            data.AllTimeClicks,
            data.ClicksInRange,
            data.UniqueVisitorsInRange,
            data.DailyClicks,
            data.TopReferrers,
            data.Browsers,
            data.Devices,
            data.Countries);
    }

    public Task<bool> SetActiveAsync(
        string code,
        bool isActive,
        CancellationToken cancellationToken) =>
        _repository.SetActiveAsync(code, isActive, cancellationToken);

    private static string GenerateCode()
    {
        var characters = new char[GeneratedCodeLength];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = CodeCharacters[RandomNumberGenerator.GetInt32(CodeCharacters.Length)];
        }

        return new string(characters);
    }

    private static ShortenedUrlResponse Map(ShortenedUrl shortenedUrl, string baseUrl) =>
        new(
            shortenedUrl.Id,
            shortenedUrl.OriginalUrl,
            BuildShortUrl(baseUrl, shortenedUrl.Code),
            shortenedUrl.Code,
            shortenedUrl.CreatedAt,
            shortenedUrl.ExpiresAt,
            shortenedUrl.IsActive,
            shortenedUrl.TotalClicks,
            shortenedUrl.LastAccessedAt);

    private static string BuildShortUrl(string baseUrl, string code) =>
        $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(code)}";

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind == DateTimeKind.Utc
            ? value
            : value.Value.ToUniversalTime();
    }

    private static string? CreateVisitorHash(ClickContext context, DateTime clickedAt)
    {
        if (string.IsNullOrWhiteSpace(context.IpAddress))
        {
            return null;
        }

        var input = $"{clickedAt:yyyy-MM-dd}|{context.IpAddress}|{context.UserAgent}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static string? GetReferrerHost(string? referrer) =>
        Uri.TryCreate(referrer, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;

    private static string DetectBrowser(string? userAgent)
    {
        var value = userAgent ?? string.Empty;
        if (value.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (value.Contains("OPR/", StringComparison.OrdinalIgnoreCase)) return "Opera";
        if (value.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (value.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (value.Contains("Safari/", StringComparison.OrdinalIgnoreCase)) return "Safari";
        if (value.Contains("PostmanRuntime/", StringComparison.OrdinalIgnoreCase)) return "Postman";
        if (value.Contains("curl/", StringComparison.OrdinalIgnoreCase)) return "curl";
        return "Other";
    }

    private static string DetectDevice(string? userAgent)
    {
        var value = userAgent ?? string.Empty;
        if (value.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("spider", StringComparison.OrdinalIgnoreCase)) return "Bot";
        if (value.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Tablet", StringComparison.OrdinalIgnoreCase)) return "Tablet";
        if (value.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "Mobile";
        return "Desktop";
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        var value = countryCode?.Trim();
        return value is { Length: 2 } && value.All(char.IsLetter)
            ? value.ToUpperInvariant()
            : null;
    }
}
