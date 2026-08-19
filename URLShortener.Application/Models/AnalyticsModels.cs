using URLShortener.Application.DTOs;

namespace URLShortener.Application.Models;

public sealed record ClickContext(
    string? IpAddress,
    string? UserAgent,
    string? Referrer,
    string? CountryCode);

public enum LinkResolutionStatus
{
    Found,
    NotFound,
    Unavailable
}

public sealed record LinkResolution(LinkResolutionStatus Status, string? OriginalUrl = null);

public sealed record LinkAnalyticsData(
    string Code,
    int AllTimeClicks,
    int ClicksInRange,
    int UniqueVisitorsInRange,
    IReadOnlyList<DailyClickItem> DailyClicks,
    IReadOnlyList<AnalyticsBreakdownItem> TopReferrers,
    IReadOnlyList<AnalyticsBreakdownItem> Browsers,
    IReadOnlyList<AnalyticsBreakdownItem> Devices,
    IReadOnlyList<AnalyticsBreakdownItem> Countries);
