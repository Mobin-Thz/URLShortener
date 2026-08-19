namespace URLShortener.Application.DTOs;

public sealed record ShortenedUrlResponse(
    Guid Id,
    string OriginalUrl,
    string ShortUrl,
    string Code,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    bool IsActive,
    int TotalClicks,
    DateTime? LastAccessedAt);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record SetLinkStatusRequest(bool IsActive);

public sealed record AnalyticsBreakdownItem(string Value, int Clicks);

public sealed record DailyClickItem(DateTime Date, int Clicks);

public sealed record LinkAnalyticsResponse(
    string Code,
    string ShortUrl,
    DateTime From,
    DateTime To,
    int AllTimeClicks,
    int ClicksInRange,
    int UniqueVisitorsInRange,
    IReadOnlyList<DailyClickItem> DailyClicks,
    IReadOnlyList<AnalyticsBreakdownItem> TopReferrers,
    IReadOnlyList<AnalyticsBreakdownItem> Browsers,
    IReadOnlyList<AnalyticsBreakdownItem> Devices,
    IReadOnlyList<AnalyticsBreakdownItem> Countries);
