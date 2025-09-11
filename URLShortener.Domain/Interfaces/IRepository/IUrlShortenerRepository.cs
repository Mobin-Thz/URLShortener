using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using URLShortener.Domain.Entities;

namespace URLShortener.Domain.Interfaces.IRepository
{
    public interface IUrlShortenerRepository
    {
        Task<string> GenerateUniquecode();
        Task AddUrlCode(ShortenedUrl shortenedUrl);
        Task<string?> GetOriginalUrlByCode(string shortCode);
    }
}
