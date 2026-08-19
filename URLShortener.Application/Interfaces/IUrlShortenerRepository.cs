using URLShortener.Application.Models;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Interfaces;

public interface IUrlShortenerRepository
{
    Task<bool> TryAddAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken);
    Task<ShortenedUrl?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShortenedUrl>> ListAsync(int skip, int take, CancellationToken cancellationToken);
    Task<long> CountAsync(CancellationToken cancellationToken);
    Task RecordClickAsync(ShortenedUrl shortenedUrl, ClickEvent clickEvent, CancellationToken cancellationToken);
    Task<LinkAnalyticsData?> GetAnalyticsAsync(
        string code,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);
    Task<bool> SetActiveAsync(string code, bool isActive, CancellationToken cancellationToken);
}
