using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using URLShortener.Domain.Entities;

namespace URLShortener.Application.Services
{
    public interface IUrlShortenerService
    {
        Task<string> GenerateUniqueCode();
        Task AddUrlCode(ShortenedUrl shortenedUrl);
        Task<string?> GetOriginalUrlByCode(string shortCode);
    }
}
