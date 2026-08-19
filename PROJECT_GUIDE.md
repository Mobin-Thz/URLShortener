# URL Shortener Project Guide

This document explains how the project is structured, how requests move through it, and why the important design decisions were made. Open `URLShortener.canvas` in Obsidian or another JSON Canvas-compatible application for the visual version.

## 1. Project goal

The application turns a long HTTP or HTTPS URL into a short link and records analytics whenever that link is followed.

It is designed as a backend learning project that demonstrates more than CRUD:

- Layered architecture and dependency inversion
- Input validation and REST API design
- Secure identifier generation
- Database uniqueness and concurrency handling
- Redirect behavior and link lifecycle rules
- Event-based analytics storage
- SQL aggregation, pagination, and indexing
- Privacy-conscious visitor counting
- Rate limiting, migrations, tests, and containers

## 2. Solution structure

```text
URLShortener.API
├── Controllers/LinksController.cs
├── Controllers/RedirectController.cs
└── Program.cs

URLShortener.Web
├── Clients/UrlShortenerApiClient.cs
├── Pages/Index.cshtml
├── Pages/Analytics.cshtml
└── wwwroot/

URLShortener.Application
├── DTOs/
├── Exceptions/
├── Interfaces/IUrlShortenerRepository.cs
├── Models/
└── Services/UrlShortenerService.cs

URLShortener.Domain
└── Entities/
    ├── ShortenedUrl.cs
    └── ClickEvent.cs

URLShortener.Infrastructure
├── Context/AppDbContext.cs
├── Repositories/UrlShortenerRepository.cs
└── Migrations/

URLShortener.Tests
├── UrlShortenerServiceTests.cs
└── UrlShortenerApiClientTests.cs
```

### Web UI layer

`URLShortener.Web` is a separate Razor Pages application. It never references Application, Domain, Infrastructure, or `AppDbContext`. Its typed `HttpClient` consumes the API's JSON contract just like an external application would.

This makes the UI replaceable: a React application, mobile client, or another backend service can use the same API without changing the URL shortener's business or persistence layers.

### API layer

The API layer translates HTTP requests into application calls. It owns routing, HTTP status codes, request metadata, Swagger, rate limiting, and dependency registration.

It should not contain persistence logic or decide how analytics are calculated.

### Application layer

The Application layer contains the use cases:

- Create a short link
- Resolve a link and record a visit
- List and inspect links
- Activate or deactivate a link
- Retrieve analytics

It defines `IUrlShortenerRepository`, which is the persistence contract required by these use cases. Infrastructure implements that contract.

### Domain layer

The Domain layer contains the central business objects and rules. It has no dependency on ASP.NET Core, Entity Framework Core, or SQL Server.

`ShortenedUrl.CanRedirectAt(...)` is a domain rule: a link may redirect only when it is active and has not expired.

### Infrastructure layer

Infrastructure contains technology-specific code:

- EF Core mappings
- SQL Server queries
- Transactions
- Database aggregation
- Unique-constraint handling
- Schema migrations

The API references Infrastructure only in the composition root (`Program.cs`) so it can register the concrete repository and database context.

## 3. Dependency direction

```text
Web UI ── HTTP + JSON ──> API
                              │
API ───────────────> Application ───────────────> Domain
 │                         ▲                         ▲
 └── composition ──> Infrastructure ────────────────┘
                            │
                            └── implements the Application repository interface
```

The important rules are that Application does not know about `AppDbContext` or SQL Server, and Web does not know about backend implementation assemblies. Application communicates through `IUrlShortenerRepository`; Web communicates through HTTP.

## 4. Data model

### ShortenedUrl

| Field | Purpose |
|---|---|
| `Id` | Internal primary key |
| `OriginalUrl` | Destination URL |
| `ShortUrl` | Legacy persisted public URL, preserved for database compatibility |
| `Code` | Unique public code or custom alias |
| `CreatedAt` | UTC creation timestamp |
| `ExpiresAt` | Optional UTC expiration timestamp |
| `IsActive` | Allows a link to be disabled without deleting it |
| `TotalClicks` | Fast all-time click counter |
| `LastAccessedAt` | Timestamp of the most recent successful redirect |

### ClickEvent

| Field | Purpose |
|---|---|
| `ShortenedUrlId` | Link being visited |
| `ClickedAt` | UTC event timestamp |
| `VisitorHash` | Daily one-way visitor fingerprint |
| `ReferrerHost` | Host that referred the visitor, when available |
| `Browser` | Small browser classification |
| `DeviceType` | Desktop, mobile, tablet, or bot |
| `CountryCode` | Optional two-letter proxy-provided country code |

The link table stores current state. The click table stores historical events. Keeping these responsibilities separate allows analytics to grow without making the main link record increasingly complex.

## 5. Create-link flow

Endpoint:

```text
POST /api/links
```

Flow:

1. ASP.NET Core validates the request DTO.
2. Only absolute `http://` and `https://` destinations are accepted.
3. If a custom alias is supplied, its length and characters are validated.
4. Otherwise, the service generates a seven-character code with `RandomNumberGenerator`.
5. The repository attempts the insert directly.
6. SQL Server's unique index is the final authority on whether the code is available.
7. A duplicate custom alias becomes `409 Conflict`.
8. A random collision causes another code to be generated and retried.
9. A successful insert returns `201 Created` with the new link details.

The database insert—not a separate existence query—decides uniqueness. This avoids the race condition where two requests check the same code before either inserts it.

## 6. Redirect and tracking flow

Endpoint:

```text
GET /{code}
```

Flow:

1. The repository finds the link by its unique code.
2. Missing links return `404 Not Found`.
3. Inactive or expired links return `410 Gone`.
4. The application classifies the request metadata.
5. A database transaction atomically:
   - Increments `TotalClicks`
   - Updates `LastAccessedAt`
   - Inserts a `ClickEvent`
6. The API responds with `302 Found` and redirects to the original URL.

The counter update uses an SQL-side increment instead of reading a number, incrementing it in memory, and writing it back. This prevents lost updates when multiple visitors arrive simultaneously.

## 7. Analytics flow

Endpoint:

```text
GET /api/links/{code}/analytics
```

Optional date range:

```text
GET /api/links/{code}/analytics?from=2026-08-01T00:00:00Z&to=2026-09-01T00:00:00Z
```

The default range is the last 30 days. The maximum accepted range is 366 days, and `to` is exclusive.

EF Core translates the analytics queries into SQL aggregation. The application does not load every event into memory.

The response contains:

- All-time click count
- Clicks during the requested range
- Approximate unique visitors during the range
- Daily click totals
- Top referrers
- Browser distribution
- Device distribution
- Country distribution

## 8. Visitor privacy

Raw IP addresses are not saved. The service creates a fingerprint using the click date, IP address, and user agent, then hashes it with SHA-256:

```text
SHA-256(date | IP address | user agent)
```

Including the date intentionally prevents the value from becoming a permanent cross-day tracking identifier. The result is an approximate visitor metric rather than a user identity system.

Country information comes from `CF-IPCountry` or `X-Country-Code`. Those values are meaningful only when a trusted reverse proxy overwrites the headers; direct clients can spoof them.

## 9. API summary

| Method | Path | Result |
|---|---|---|
| `POST` | `/api/links` | Create a link |
| `GET` | `/api/links` | Paginated link list |
| `GET` | `/api/links/{code}` | Link metadata |
| `PUT` | `/api/links/{code}/status` | Activate or deactivate |
| `GET` | `/api/links/{code}/analytics` | Analytics summary |
| `GET` | `/{code}` | Track and redirect |
| `GET` | `/health` | Health probe |

## 10. HTTP status behavior

| Status | Meaning |
|---|---|
| `201 Created` | A short link was created |
| `204 No Content` | Link status was changed |
| `302 Found` | A short link redirected successfully |
| `400 Bad Request` | Validation or date-range error |
| `404 Not Found` | Code does not exist |
| `409 Conflict` | Custom alias is already taken |
| `410 Gone` | Link is inactive or expired |
| `429 Too Many Requests` | Creation rate limit was exceeded |

## 11. Database migration

`20260819090000_AddAnalytics` is an additive, non-destructive migration for existing data. It:

- Expands `Code` from 6 to 32 characters
- Adds expiration, status, click-count, and last-access fields
- Creates `ClickEvents`
- Adds indexes for link/time analytics and visitor hashes
- Preserves the existing `ShortUrl` column

Rolling back to the original schema after creating codes longer than six characters requires cleaning up or shortening those codes first.

## 12. Running the project

Apply the migration from Visual Studio's Package Manager Console:

```powershell
Update-Database -Project URLShortener.Infrastructure -StartupProject URLShortener.API
```

Run the API and UI in separate terminals:

```powershell
dotnet run --project URLShortener.API --launch-profile http
dotnet run --project URLShortener.Web --launch-profile http
```

Open `http://localhost:5260`. The Web project reads the API location from `ApiBaseUrl`, which defaults to `http://localhost:5137/`.

Useful request examples are available in:

- `URLShortener.API/URLShortener.API.http`
- `PostmanCollection/URLShortener.postman_collection.json`
- Swagger while running in Development

## 13. Tests

Run:

```powershell
dotnet test URLShortener.sln
```

The tests cover:

- Custom alias creation
- Duplicate aliases
- Click classification and privacy hashing
- Expired-link behavior
- URL scheme validation
- EF Core migration discovery
- UI API contract deserialization and ProblemDetails error handling

## 14. Recommended code-reading order

1. `URLShortener.Domain/Entities/ShortenedUrl.cs`
2. `URLShortener.Domain/Entities/ClickEvent.cs`
3. `URLShortener.Application/DTOs/ShortenUrlRequest.cs`
4. `URLShortener.Application/Services/UrlShortenerService.cs`
5. `URLShortener.Application/Interfaces/IUrlShortenerRepository.cs`
6. `URLShortener.Infrastructure/Repositories/UrlShortenerRepository.cs`
7. `URLShortener.API/Controllers/LinksController.cs`
8. `URLShortener.API/Controllers/RedirectController.cs`
9. `URLShortener.API/Program.cs`
10. `URLShortener.Web/Clients/UrlShortenerApiClient.cs`
11. `URLShortener.Web/Pages/Index.cshtml.cs`
12. `URLShortener.Web/Pages/Analytics.cshtml.cs`
13. `URLShortener.Tests/UrlShortenerServiceTests.cs`
14. `URLShortener.Tests/UrlShortenerApiClientTests.cs`

## 15. Current tradeoffs and next steps

This is a complete learning project, but public production deployment would need additional decisions:

- Add authentication and per-user link ownership
- Protect management and analytics endpoints with authorization
- Configure a canonical public base URL instead of trusting the request host
- Use a secret-keyed HMAC for visitor fingerprints
- Trust forwarded and country headers only from known proxies
- Move high-volume click ingestion to a background queue
- Add retention or aggregation policies for old events
- Add Redis caching for popular redirects
- Add integration tests against a disposable SQL Server instance

These are intentionally described as extensions instead of being hidden inside the current learning scope.
