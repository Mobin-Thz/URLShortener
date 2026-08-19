using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using URLShortener.Web.Clients;
using URLShortener.Web.Models;

namespace URLShortener.Web.Pages;

public sealed class AnalyticsModel : PageModel
{
    private readonly UrlShortenerApiClient _apiClient;

    public AnalyticsModel(UrlShortenerApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public LinkAnalyticsView? Analytics { get; private set; }
    public string? ServiceError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            return RedirectToPage("/Index");
        }

        DateTime? fromUtc = From is null ? null : DateTime.SpecifyKind(From.Value.Date, DateTimeKind.Utc);
        DateTime? toUtc = To is null ? null : DateTime.SpecifyKind(To.Value.Date, DateTimeKind.Utc);

        if (fromUtc is not null && toUtc is not null && fromUtc >= toUtc)
        {
            ModelState.AddModelError(nameof(To), "To must be later than From.");
            return Page();
        }

        try
        {
            Analytics = await _apiClient.GetAnalyticsAsync(
                Code.Trim(),
                fromUtc,
                toUtc,
                cancellationToken);

            if (Analytics is null)
            {
                return NotFound();
            }

            return Page();
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ServiceError = exception switch
            {
                ApiClientException apiException => apiException.Message,
                TaskCanceledException => "The API did not respond before the request timed out.",
                _ => "The URL Shortener API is unavailable. Start URLShortener.API and try again."
            };
            return Page();
        }
    }
}
