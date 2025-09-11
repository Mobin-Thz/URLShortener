using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using URLShortener.Application.Services;
using URLShortener.Domain.Entities;
using URLShortener.Infrastructure.Repositories;

namespace URLShortener.Infrastructure.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Define DbSets for your entities here
         public DbSet<ShortenedUrl> ShortenedUrls { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure entity properties and relationships here if needed
            modelBuilder.Entity<ShortenedUrl>(builder =>
            {
                builder.HasIndex(e => e.Code).IsUnique();
                builder.Property(e => e.Code).HasMaxLength(UrlShortenerRepository.CodeLength);

            });
        }
    }
}
