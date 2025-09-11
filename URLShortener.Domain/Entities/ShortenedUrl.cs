using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace URLShortener.Domain.Entities
{
    public class ShortenedUrl
    {
        public Guid Id { get; set; }

        public string OriginalUrl { get; set; }

        public  string ShortUrl { get; set; }

        public string Code { get; set; }

        public DateTime CreatedAt { get; set; }


    }
}
