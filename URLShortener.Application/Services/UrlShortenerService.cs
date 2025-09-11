using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using URLShortener.Domain.Entities;
using URLShortener.Domain.Interfaces.IRepository;

namespace URLShortener.Application.Services
{
    public class UrlShortenerService : IUrlShortenerService
    {

        private readonly IUrlShortenerRepository _urlShortenerRepository;
        public UrlShortenerService(IUrlShortenerRepository urlShortenerRepository)
        {
            _urlShortenerRepository = urlShortenerRepository;
        }


        public async Task<string> GenerateUniqueCode()
        {
          return await _urlShortenerRepository.GenerateUniquecode();
        }

        public async Task AddUrlCode(ShortenedUrl shortenedUrl)
        {
            await _urlShortenerRepository.AddUrlCode(shortenedUrl);
        }


        public async Task<string?> GetOriginalUrlByCode(string shortCode)
        {
            return await _urlShortenerRepository.GetOriginalUrlByCode(shortCode);
        }


    }
}
