using Microsoft.AspNetCore.Mvc;
using URLShortener.Application.DTOs;
using URLShortener.Application.Services;
using URLShortener.Domain.Entities;

namespace URLShortener.API.Controllers
{
    [ApiController]
    [Route("api/[Controller]/[Action]")]
    public class URLShortenerController : ControllerBase
    {
        private readonly IUrlShortenerService _urlShorteningService;

        public URLShortenerController(IUrlShortenerService urlShorteningService)
        {
            _urlShorteningService = urlShorteningService;
        }

        [HttpPost]
        public async Task<ActionResult<string>> Shorten([FromBody] ShortenUrlRequest request)
        {
            if (!Uri.IsWellFormedUriString(request.OriginalUrl, UriKind.Absolute))
            {
                return BadRequest("Invalid URL format.");
            }

            var code = await _urlShorteningService.GenerateUniqueCode();

            var shortenedUrl = new ShortenedUrl
            {
                Id = Guid.NewGuid(),
                OriginalUrl = request.OriginalUrl,
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ShortUrl = $"{Request.Scheme}://{Request.Host}/{code}"
            };
            await _urlShorteningService.AddUrlCode(shortenedUrl);

            return Ok(shortenedUrl.ShortUrl);
        }

        [HttpGet("{shortCode}")]
        public async Task<IActionResult> RedirectToOriginal(string shortCode)
        {
            var originalUrl = await _urlShorteningService.GetOriginalUrlByCode(shortCode);

            if (originalUrl == null)
            {
                return NotFound("URL not found.");
            }

            return Redirect(originalUrl); // 302 Found
        }

    }
    
}
