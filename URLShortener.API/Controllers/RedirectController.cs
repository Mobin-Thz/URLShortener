using Microsoft.AspNetCore.Mvc;
using URLShortener.Application.Models;
using URLShortener.Application.Services;

namespace URLShortener.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class RedirectController : ControllerBase
{
    private readonly IUrlShortenerService _service;

    public RedirectController(IUrlShortenerService service)
    {
        _service = service;
    }

    [HttpGet("{code:minlength(4)}")]
    public async Task<IActionResult> RedirectToOriginal(
        string code,
        CancellationToken cancellationToken)
    {
        var countryCode = Request.Headers["CF-IPCountry"].FirstOrDefault()
            ?? Request.Headers["X-Country-Code"].FirstOrDefault();

        var clickContext = new ClickContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            Request.Headers.Referer.ToString(),
            countryCode);

        var result = await _service.ResolveAsync(code, clickContext, cancellationToken);
        return result.Status switch
        {
            LinkResolutionStatus.Found => Redirect(result.OriginalUrl!),
            LinkResolutionStatus.Unavailable => Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Link unavailable",
                detail: "This short link is inactive or has expired."),
            _ => NotFound()
        };
    }
}
