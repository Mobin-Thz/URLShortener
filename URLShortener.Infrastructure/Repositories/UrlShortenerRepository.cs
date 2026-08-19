using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using URLShortener.Application.DTOs;
using URLShortener.Application.Interfaces;
using URLShortener.Application.Models;
using URLShortener.Domain.Entities;
using URLShortener.Infrastructure.Context;

namespace URLShortener.Infrastructure.Repositories;

public sealed class UrlShortenerRepository : IUrlShortenerRepository
{
    private readonly AppDbContext _dbContext;

    public UrlShortenerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryAddAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken)
    {
        _dbContext.ShortenedUrls.Add(shortenedUrl);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _dbContext.Entry(shortenedUrl).State = EntityState.Detached;
            return false;
        }
    }

    public Task<ShortenedUrl?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        _dbContext.ShortenedUrls
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == code, cancellationToken);

    public async Task<IReadOnlyList<ShortenedUrl>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await _dbContext.ShortenedUrls
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        _dbContext.ShortenedUrls.LongCountAsync(cancellationToken);

    public async Task RecordClickAsync(
        ShortenedUrl shortenedUrl,
        ClickEvent clickEvent,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var updatedRows = await _dbContext.ShortenedUrls
            .Where(item => item.Id == shortenedUrl.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.TotalClicks, item => item.TotalClicks + 1)
                    .SetProperty(item => item.LastAccessedAt, clickEvent.ClickedAt),
                cancellationToken);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException("The shortened URL disappeared while recording analytics.");
        }

        _dbContext.ClickEvents.Add(clickEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<LinkAnalyticsData?> GetAnalyticsAsync(
        string code,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var link = await _dbContext.ShortenedUrls
            .AsNoTracking()
            .Where(item => item.Code == code)
            .Select(item => new { item.Id, item.Code, item.TotalClicks })
            .SingleOrDefaultAsync(cancellationToken);

        if (link is null)
        {
            return null;
        }

        var clicks = _dbContext.ClickEvents
            .AsNoTracking()
            .Where(item =>
                item.ShortenedUrlId == link.Id &&
                item.ClickedAt >= from &&
                item.ClickedAt < to);

        var clicksInRange = await clicks.CountAsync(cancellationToken);
        var uniqueVisitors = await clicks
            .Where(item => item.VisitorHash != null)
            .Select(item => item.VisitorHash)
            .Distinct()
            .CountAsync(cancellationToken);

        var dailyClicks = await clicks
            .GroupBy(item => item.ClickedAt.Date)
            .Select(group => new DailyClickItem(group.Key, group.Count()))
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);

        var referrers = await clicks
            .GroupBy(item => item.ReferrerHost ?? "Direct")
            .Select(group => new AnalyticsBreakdownItem(group.Key, group.Count()))
            .OrderByDescending(item => item.Clicks)
            .ThenBy(item => item.Value)
            .Take(10)
            .ToListAsync(cancellationToken);

        var browsers = await clicks
            .GroupBy(item => item.Browser)
            .Select(group => new AnalyticsBreakdownItem(group.Key, group.Count()))
            .OrderByDescending(item => item.Clicks)
            .ThenBy(item => item.Value)
            .ToListAsync(cancellationToken);

        var devices = await clicks
            .GroupBy(item => item.DeviceType)
            .Select(group => new AnalyticsBreakdownItem(group.Key, group.Count()))
            .OrderByDescending(item => item.Clicks)
            .ThenBy(item => item.Value)
            .ToListAsync(cancellationToken);

        var countries = await clicks
            .GroupBy(item => item.CountryCode ?? "Unknown")
            .Select(group => new AnalyticsBreakdownItem(group.Key, group.Count()))
            .OrderByDescending(item => item.Clicks)
            .ThenBy(item => item.Value)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new LinkAnalyticsData(
            link.Code,
            link.TotalClicks,
            clicksInRange,
            uniqueVisitors,
            dailyClicks,
            referrers,
            browsers,
            devices,
            countries);
    }

    public async Task<bool> SetActiveAsync(
        string code,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var updatedRows = await _dbContext.ShortenedUrls
            .Where(item => item.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.IsActive, isActive),
                cancellationToken);

        return updatedRows == 1;
    }
}
