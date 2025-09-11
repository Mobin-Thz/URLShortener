using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace URLShortener.Application.DTOs
{
    public class ShortenUrlRequest
    {
        public string OriginalUrl { get; set; } = string.Empty;
    }
}
