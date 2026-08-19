using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.Application.DTOs;
using URLShortener.Application.Exceptions;
using URLShortener.Application.Services;

namespace URLShortener.API.Controllers;

[ApiController]
[Route("api/links")]
public sealed class LinksController : ControllerBase
{
    private readonly IUrlShortenerService _service;

    public LinksController(IUrlShortenerService service)
    {
        _service = service;
    }

    [HttpPost]
    [EnableRateLimiting("link-creation")]
    [ProducesResponseType<ShortenedUrlResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShortenedUrlResponse>> Create(
        [FromBody] ShortenUrlRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _service.CreateAsync(request, GetBaseUrl(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { code = response.Code }, response);
        }
        catch (AliasAlreadyExistsException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Alias already exists",
                Detail = exception.Message
            });
        }
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<ShortenedUrlResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ShortenedUrlResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            ModelState.AddModelError(nameof(pageSize), "PageSize must be between 1 and 100.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _service.ListAsync(page, pageSize, GetBaseUrl(), cancellationToken));
    }

    [HttpGet("{code}")]
    [ProducesResponseType<ShortenedUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShortenedUrlResponse>> Get(
        string code,
        CancellationToken cancellationToken)
    {
        var response = await _service.GetAsync(code, GetBaseUrl(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{code}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        string code,
        [FromBody] SetLinkStatusRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.SetActiveAsync(code, request.IsActive, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpGet("{code}/analytics")]
    [ProducesResponseType<LinkAnalyticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LinkAnalyticsResponse>> GetAnalytics(
        string code,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var rangeEnd = ToUtc(to ?? DateTime.UtcNow);
        var rangeStart = ToUtc(from ?? rangeEnd.AddDays(-30));

        if (rangeStart >= rangeEnd)
        {
            ModelState.AddModelError(nameof(from), "From must be earlier than To.");
        }

        if (rangeEnd - rangeStart > TimeSpan.FromDays(366))
        {
            ModelState.AddModelError(nameof(from), "The analytics range cannot exceed 366 days.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var response = await _service.GetAnalyticsAsync(
            code,
            rangeStart,
            rangeEnd,
            GetBaseUrl(),
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    private string GetBaseUrl() =>
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
