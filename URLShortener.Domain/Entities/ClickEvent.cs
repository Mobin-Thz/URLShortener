namespace URLShortener.Domain.Entities;

public sealed class ClickEvent
{
    public long Id { get; set; }
    public Guid ShortenedUrlId { get; set; }
    public ShortenedUrl ShortenedUrl { get; set; } = null!;
    public DateTime ClickedAt { get; set; }
    public string? VisitorHash { get; set; }
    public string? ReferrerHost { get; set; }
    public required string Browser { get; set; }
    public required string DeviceType { get; set; }
    public string? CountryCode { get; set; }
}
