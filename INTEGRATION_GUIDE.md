# Using the URL Shortener in Other Projects

The URL shortener is designed to be reused through its HTTP API. The new `URLShortener.Web` project is one client of that API, not part of the backend's database or business layers.

## Recommended boundary

```text
Your website / mobile app / service
              │
              │ HTTPS + JSON
              ▼
       URLShortener.API
              │
              ▼
          SQL Server
```

Do not let another application connect directly to the URL shortener database. Calling the API preserves validation, collision handling, rate limiting, lifecycle rules, and analytics tracking.

## API operations

| Operation | HTTP request |
|---|---|
| Create link | `POST /api/links` |
| List links | `GET /api/links?page=1&pageSize=20` |
| Get metadata | `GET /api/links/{code}` |
| Change status | `PUT /api/links/{code}/status` |
| Read analytics | `GET /api/links/{code}/analytics` |
| Follow short link | `GET /{code}` |

## Example: call it from another .NET project

Register a typed client:

```csharp
builder.Services.AddHttpClient<ShortLinkClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:UrlShortener"]!);
});
```

Create a small client:

```csharp
using System.Net.Http.Json;

public sealed class ShortLinkClient(HttpClient httpClient)
{
    public async Task<ShortLinkResponse?> CreateAsync(
        string destination,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/links",
            new { originalUrl = destination },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShortLinkResponse>(
            cancellationToken);
    }
}

public sealed record ShortLinkResponse(
    Guid Id,
    string OriginalUrl,
    string ShortUrl,
    string Code);
```

Configuration:

```json
{
  "Services": {
    "UrlShortener": "https://links.example.com/"
  }
}
```

The complete implementation in `URLShortener.Web/Clients/UrlShortenerApiClient.cs` demonstrates pagination, errors, status updates, and analytics as well.

## Example: call it from JavaScript

```javascript
const response = await fetch("https://links.example.com/api/links", {
  method: "POST",
  headers: {
    "Content-Type": "application/json"
  },
  body: JSON.stringify({
    originalUrl: "https://example.com/orders/123"
  })
});

if (!response.ok) {
  throw new Error(`Shortener returned ${response.status}`);
}

const link = await response.json();
console.log(link.shortUrl);
```

If browser JavaScript calls the API from a different origin, configure a restrictive CORS policy in `URLShortener.API`. The included Razor UI makes server-to-server calls, so it does not require CORS.

## Useful product integrations

### Shareable resources

Create short links for reports, files, invoices, support tickets, dashboards, or public profiles.

### Campaign attribution

Create separate aliases for channels such as email, Telegram, QR codes, or partner campaigns, then compare their analytics.

### Invitations and onboarding

Shorten application invitation URLs. For security-sensitive invitations, the destination should still contain a separate single-use token and enforce its own expiration.

### Notifications

Let an email, SMS, or notification service create readable links before sending messages.

### QR codes

Generate QR codes from the returned `shortUrl`. The destination can later change only if a future destination-update endpoint is deliberately added.

## Deployment model

A typical deployment uses three components:

```text
Reverse proxy / TLS
├── links.example.com       → URLShortener.API
└── links-admin.example.com → URLShortener.Web
                              │
                              └── calls URLShortener.API privately
```

Set the Web application's API address through:

```text
ApiBaseUrl=https://links.example.com/
```

In Docker Compose, the Web container uses the internal service address `http://api:8080/`.

## Before using it publicly

The current project is optimized for learning and local testing. Add these controls before using it as a shared production service:

1. Authentication for management operations
2. Per-user or per-application link ownership
3. Authorization around list, status, and analytics endpoints
4. API keys or OAuth client credentials for other services
5. A configured canonical public URL instead of deriving it from the request host
6. HTTPS at the reverse proxy
7. Restrictive CORS only for approved browser origins
8. Secret-keyed visitor fingerprints
9. Data-retention rules for click events
10. Monitoring and backups

## When to extract a client SDK

If several .NET projects use the service, extract the API records and typed client into a small package such as:

```text
URLShortener.Client
├── UrlShortenerApiClient.cs
└── Contracts/
```

Publish that package internally. Keep Domain, Infrastructure, and `AppDbContext` private to the URL shortener service.
