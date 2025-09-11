using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using URLShortener.Application.Services;
using URLShortener.Domain.Entities;
using URLShortener.Domain.Interfaces.IRepository;
using URLShortener.Infrastructure.Context;

namespace URLShortener.Infrastructure.Repositories
{
    public class UrlShortenerRepository : IUrlShortenerRepository
    {
        private const string Characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        public const int CodeLength = 6;

        private readonly Random _random = new Random();

        private readonly AppDbContext _dbContext;
        public UrlShortenerRepository(AppDbContext appdbContext)
        {
            _dbContext = appdbContext;
        }

        public async Task<string> GenerateUniquecode()
        {
            while (true)
            {
                var codeChars = new char[CodeLength];

                for (int i = 0; i < CodeLength; i++)
                {
                    var randomindex = _random.Next(Characters.Length - 1);
                    codeChars[i] = Characters[randomindex];
                }

                var code = new string(codeChars);

                if (!await _dbContext.ShortenedUrls.AnyAsync(s => s.Code == code))
                {
                    return code;
                }


            }
        }


        public async Task AddUrlCode(ShortenedUrl shortenedUrl)
        {
            await _dbContext.ShortenedUrls.AddAsync(shortenedUrl);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<string?> GetOriginalUrlByCode(string shortCode)
        {
            var shortenedUrl = await _dbContext.ShortenedUrls.FirstOrDefaultAsync(s => s.Code == shortCode);
            return shortenedUrl?.OriginalUrl;


        }
    }

}

