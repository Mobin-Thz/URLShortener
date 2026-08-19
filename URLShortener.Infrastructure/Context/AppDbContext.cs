using Microsoft.EntityFrameworkCore;
using URLShortener.Domain.Entities;

namespace URLShortener.Infrastructure.Context;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ShortenedUrl> ShortenedUrls => Set<ShortenedUrl>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortenedUrl>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => item.Code).IsUnique();
            builder.Property(item => item.Code).HasMaxLength(32).IsRequired();
            builder.Property(item => item.OriginalUrl).IsRequired();
            builder.Property(item => item.ShortUrl).IsRequired();
            builder.Property(item => item.IsActive).HasDefaultValue(true);
            builder.Property(item => item.TotalClicks).HasDefaultValue(0);
        });

        modelBuilder.Entity<ClickEvent>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.VisitorHash).HasMaxLength(64);
            builder.Property(item => item.ReferrerHost).HasMaxLength(255);
            builder.Property(item => item.Browser).HasMaxLength(64).IsRequired();
            builder.Property(item => item.DeviceType).HasMaxLength(32).IsRequired();
            builder.Property(item => item.CountryCode).HasMaxLength(2);
            builder.HasIndex(item => new { item.ShortenedUrlId, item.ClickedAt });
            builder.HasIndex(item => item.VisitorHash);
            builder.HasOne(item => item.ShortenedUrl)
                .WithMany(item => item.Clicks)
                .HasForeignKey(item => item.ShortenedUrlId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
