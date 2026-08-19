using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using URLShortener.Application.DTOs;
using URLShortener.Application.Exceptions;
using URLShortener.Application.Interfaces;
using URLShortener.Application.Models;
using URLShortener.Application.Services;
using URLShortener.Domain.Entities;
using URLShortener.Infrastructure.Context;
using Xunit;

namespace URLShortener.Tests;

public sealed class UrlShortenerServiceTests
{
    [Fact]
    public async Task CreateAsync_WithCustomAlias_CreatesExpectedLink()
    {
        var repository = new FakeRepository();
        var service = new UrlShortenerService(repository);

        var result = await service.CreateAsync(
            new ShortenUrlRequest
            {
                OriginalUrl = "https://example.com/long/path",
                CustomAlias = "docs"
            },
            "https://sho.rt",
            CancellationToken.None);

        Assert.Equal("docs", result.Code);
        Assert.Equal("https://sho.rt/docs", result.ShortUrl);
        Assert.True(result.IsActive);
        Assert.Single(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateAlias_ThrowsConflictException()
    {
        var repository = new FakeRepository();
        repository.Links.Add(CreateLink("taken"));
        var service = new UrlShortenerService(repository);

        await Assert.ThrowsAsync<AliasAlreadyExistsException>(() => service.CreateAsync(
            new ShortenUrlRequest
            {
                OriginalUrl = "https://example.com",
                CustomAlias = "taken"
            },
            "https://sho.rt",
            CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_ForActiveLink_RecordsPrivacyAwareAnalytics()
    {
        var repository = new FakeRepository();
        repository.Links.Add(CreateLink("active"));
        var service = new UrlShortenerService(repository);

        var result = await service.ResolveAsync(
            "active",
            new ClickContext(
                "203.0.113.10",
                "Mozilla/5.0 (iPhone) AppleWebKit Chrome/120 Mobile",
                "https://news.example/article",
                "ir"),
            CancellationToken.None);

        Assert.Equal(LinkResolutionStatus.Found, result.Status);
        Assert.Equal("https://example.com", result.OriginalUrl);
        Assert.NotNull(repository.LastClick);
        Assert.Equal("Chrome", repository.LastClick.Browser);
        Assert.Equal("Mobile", repository.LastClick.DeviceType);
        Assert.Equal("news.example", repository.LastClick.ReferrerHost);
        Assert.Equal("IR", repository.LastClick.CountryCode);
        Assert.Equal(64, repository.LastClick.VisitorHash?.Length);
    }

    [Fact]
    public async Task ResolveAsync_ForExpiredLink_DoesNotRecordClick()
    {
        var repository = new FakeRepository();
        var link = CreateLink("old");
        link.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        repository.Links.Add(link);
        var service = new UrlShortenerService(repository);

        var result = await service.ResolveAsync(
            "old",
            new ClickContext(null, null, null, null),
            CancellationToken.None);

        Assert.Equal(LinkResolutionStatus.Unavailable, result.Status);
        Assert.Null(repository.LastClick);
    }

    [Fact]
    public async Task ResolveAsync_ForInactiveLink_DoesNotRecordClick()
    {
        var repository = new FakeRepository();
        var link = CreateLink("paused");
        link.IsActive = false;
        repository.Links.Add(link);
        var service = new UrlShortenerService(repository);

        var result = await service.ResolveAsync(
            "paused",
            new ClickContext("203.0.113.20", "curl/8.0", null, null),
            CancellationToken.None);

        Assert.Equal(LinkResolutionStatus.Unavailable, result.Status);
        Assert.Null(repository.LastClick);
        Assert.Equal(0, link.TotalClicks);
    }

    [Fact]
    public void ShortenUrlRequest_RejectsNonHttpSchemes()
    {
        var request = new ShortenUrlRequest { OriginalUrl = "file:///etc/passwd" };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.OriginalUrl)));
    }

    [Fact]
    public void AnalyticsMigration_IsDiscoverableByEntityFramework()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MigrationDiscoveryOnly")
            .Options;

        using var context = new AppDbContext(options);
        var migrations = context.Database.GetMigrations();

        Assert.Contains("20260819090000_AddAnalytics", migrations);
    }

    private static ShortenedUrl CreateLink(string code) => new()
    {
        Id = Guid.NewGuid(),
        OriginalUrl = "https://example.com",
        ShortUrl = $"https://sho.rt/{code}",
        Code = code,
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };

    private sealed class FakeRepository : IUrlShortenerRepository
    {
        public List<ShortenedUrl> Links { get; } = new();
        public ClickEvent? LastClick { get; private set; }

        public Task<bool> TryAddAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken)
        {
            if (Links.Any(item => item.Code == shortenedUrl.Code))
            {
                return Task.FromResult(false);
            }

            Links.Add(shortenedUrl);
            return Task.FromResult(true);
        }

        public Task<ShortenedUrl?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(Links.SingleOrDefault(item => item.Code == code));

        public Task<IReadOnlyList<ShortenedUrl>> ListAsync(
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ShortenedUrl>>(Links.Skip(skip).Take(take).ToArray());

        public Task<long> CountAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)Links.Count);

        public Task RecordClickAsync(
            ShortenedUrl shortenedUrl,
            ClickEvent clickEvent,
            CancellationToken cancellationToken)
        {
            LastClick = clickEvent;
            shortenedUrl.TotalClicks++;
            shortenedUrl.LastAccessedAt = clickEvent.ClickedAt;
            return Task.CompletedTask;
        }

        public Task<LinkAnalyticsData?> GetAnalyticsAsync(
            string code,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken) =>
            Task.FromResult<LinkAnalyticsData?>(null);

        public Task<bool> SetActiveAsync(
            string code,
            bool isActive,
            CancellationToken cancellationToken)
        {
            var link = Links.SingleOrDefault(item => item.Code == code);
            if (link is null) return Task.FromResult(false);
            link.IsActive = isActive;
            return Task.FromResult(true);
        }
    }
}
