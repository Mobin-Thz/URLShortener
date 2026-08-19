using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace URLShortener.Domain.Entities
{
    public sealed class ShortenedUrl
    {
        public Guid Id { get; set; }

        public required string OriginalUrl { get; set; }

        public required string ShortUrl { get; set; }

        public required string Code { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int TotalClicks { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public ICollection<ClickEvent> Clicks { get; } = new List<ClickEvent>();

        public bool CanRedirectAt(DateTime utcNow) =>
            IsActive && (ExpiresAt is null || ExpiresAt > utcNow);


    }
}
