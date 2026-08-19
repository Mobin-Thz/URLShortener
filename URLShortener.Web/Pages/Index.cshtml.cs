using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using URLShortener.Web.Clients;
using URLShortener.Web.Models;

namespace URLShortener.Web.Pages;

public sealed class IndexModel : PageModel
{
    private const int PageSize = 10;
    private readonly UrlShortenerApiClient _apiClient;

    public IndexModel(UrlShortenerApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public CreateLinkForm Form { get; set; } = new();

    public PagedResponse<ShortenedUrlView> Links { get; private set; } =
        new(Array.Empty<ShortenedUrlView>(), 1, PageSize, 0, 0);

    public string? ServiceError { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        await LoadLinksAsync(Math.Max(1, pageNumber), cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (Form.ExpiresAtLocal is not null && Form.ExpiresAtLocal <= DateTime.Now)
        {
            ModelState.AddModelError(
                nameof(Form.ExpiresAtLocal),
                "Expiration must be in the future.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLinksAsync(Math.Max(1, pageNumber), cancellationToken);
            return Page();
        }

        DateTime? expiresAtUtc = Form.ExpiresAtLocal is null
            ? null
            : DateTime.SpecifyKind(Form.ExpiresAtLocal.Value, DateTimeKind.Local).ToUniversalTime();

        try
        {
            var created = await _apiClient.CreateLinkAsync(
                new CreateLinkRequest(
                    Form.OriginalUrl.Trim(),
                    string.IsNullOrWhiteSpace(Form.CustomAlias) ? null : Form.CustomAlias.Trim(),
                    expiresAtUtc),
                cancellationToken);

            SuccessMessage = $"Created {created.ShortUrl}";
            return RedirectToPage("/Index");
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ServiceError = GetServiceMessage(exception);
            await LoadLinksAsync(Math.Max(1, pageNumber), cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostStatusAsync(
        string code,
        bool isActive,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest();
        }

        try
        {
            var updated = await _apiClient.SetStatusAsync(code, isActive, cancellationToken);
            if (!updated)
            {
                return NotFound();
            }

            SuccessMessage = isActive ? $"Activated {code}." : $"Paused {code}.";
            return RedirectToPage("/Index", new { pageNumber = Math.Max(1, pageNumber) });
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ServiceError = GetServiceMessage(exception);
            await LoadLinksAsync(Math.Max(1, pageNumber), cancellationToken);
            return Page();
        }
    }

    private async Task LoadLinksAsync(int page, CancellationToken cancellationToken)
    {
        try
        {
            Links = await _apiClient.ListLinksAsync(page, PageSize, cancellationToken);
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            ServiceError ??= GetServiceMessage(exception);
            Links = new PagedResponse<ShortenedUrlView>(
                Array.Empty<ShortenedUrlView>(),
                page,
                PageSize,
                0,
                0);
        }
    }

    private static string GetServiceMessage(Exception exception) => exception switch
    {
        ApiClientException apiException => apiException.Message,
        TaskCanceledException => "The API did not respond before the request timed out.",
        _ => "The URL Shortener API is unavailable. Start URLShortener.API and try again."
    };
}
