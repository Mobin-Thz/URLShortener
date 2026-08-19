using System.ComponentModel.DataAnnotations;

namespace URLShortener.Application.DTOs;

public sealed class ShortenUrlRequest : IValidatableObject
{
    [Required]
    [StringLength(2048)]
    public string OriginalUrl { get; init; } = string.Empty;

    [StringLength(32, MinimumLength = 4)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "CustomAlias may contain only letters, numbers, underscores, and hyphens.")]
    public string? CustomAlias { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(OriginalUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "OriginalUrl must be an absolute HTTP or HTTPS URL.",
                new[] { nameof(OriginalUrl) });
        }

        if (ExpiresAt is not null && ExpiresAt <= DateTime.UtcNow)
        {
            yield return new ValidationResult(
                "ExpiresAt must be in the future.",
                new[] { nameof(ExpiresAt) });
        }
    }
}
