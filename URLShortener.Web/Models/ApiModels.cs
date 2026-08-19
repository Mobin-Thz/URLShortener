using System.ComponentModel.DataAnnotations;

namespace URLShortener.Web.Models;

public sealed class CreateLinkForm
{
    [Required]
    [StringLength(2048)]
    [Display(Name = "Destination URL")]
    public string OriginalUrl { get; set; } = string.Empty;

    [StringLength(32, MinimumLength = 4)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Use only letters, numbers, underscores, and hyphens.")]
    [Display(Name = "Custom alias (optional)")]
    public string? CustomAlias { get; set; }

    [Display(Name = "Expires at (optional, local time)")]
    public DateTime? ExpiresAtLocal { get; set; }
}

public sealed record CreateLinkRequest(
    string OriginalUrl,
    string? CustomAlias,
    DateTime? ExpiresAt);

public sealed record SetLinkStatusRequest(bool IsActive);

public sealed record ShortenedUrlView(
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
    long TotalCount,
    int TotalPages);

public sealed record AnalyticsBreakdownItem(string Value, int Clicks);

public sealed record DailyClickItem(DateTime Date, int Clicks);

public sealed record LinkAnalyticsView(
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

public sealed record BreakdownPanel(
    string Title,
    IReadOnlyList<AnalyticsBreakdownItem> Items)
{
    public int Maximum => Items.Count == 0 ? 1 : Items.Max(item => item.Clicks);
}

internal sealed record ApiProblem(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);
