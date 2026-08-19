# URL Shortener with Analytics

A learning-focused URL shortener with an ASP.NET Core API, Razor Pages dashboard, Entity Framework Core, and SQL Server. It creates short links, redirects visitors, and records privacy-aware click analytics.

## What the project teaches

- Layered architecture and dependency inversion
- REST API design and validation
- Cryptographically secure short-code generation
- Unique database constraints and EF Core migrations
- Redirect handling and HTTP status codes
- Event-style analytics storage and SQL aggregation
- Pagination, expiration, activation, and custom aliases
- Rate limiting and forwarded proxy headers
- Unit testing with a fake repository
- Docker-based local deployment
- A separate server-rendered UI that consumes the API over HTTP

## Features

- Random seven-character codes or custom aliases
- Only absolute HTTP and HTTPS destinations are accepted
- Optional UTC expiration time
- Activate or deactivate an existing link
- Paginated link listing
- HTTP `302` redirects; expired or inactive links return `410 Gone`
- Creation rate limit: 10 requests per IP per minute
- All-time and date-range click totals
- Daily click series
- Approximate unique visitors
- Top referrers, browsers, device types, and countries
- Health endpoint, Swagger, Postman collection, and `.http` request file
- Dashboard for link creation, status changes, copying, pagination, and analytics

Raw IP addresses are not stored. A daily SHA-256 visitor fingerprint is created from the IP address and user agent. Country analytics use `CF-IPCountry` or `X-Country-Code`; only trust those headers when a controlled reverse proxy sets them.

## Architecture

```text
URLShortener.Web             Razor Pages UI and typed HTTP API client
        |
        | HTTP + JSON
        v
URLShortener.API             HTTP, validation, redirects, rate limiting
        |
URLShortener.Application     use cases, DTOs, analytics classification
        |
URLShortener.Domain          ShortenedUrl and ClickEvent entities
        ^
URLShortener.Infrastructure  EF Core repository, SQL aggregation, migrations
```

`ShortenedUrl` stores the current state and an all-time click counter. `ClickEvent` stores the dimensions needed for analytics. The counter is updated atomically while the event is inserted in the same transaction.

## API

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/links` | Create a short link |
| `GET` | `/api/links?page=1&pageSize=20` | List links |
| `GET` | `/api/links/{code}` | Read link details |
| `PUT` | `/api/links/{code}/status` | Activate or deactivate |
| `GET` | `/api/links/{code}/analytics` | Last 30 days of analytics |
| `GET` | `/api/links/{code}/analytics?from=...&to=...` | Custom UTC range; `to` is exclusive |
| `GET` | `/{code}` | Redirect and record a click |
| `GET` | `/health` | Health probe |

Create request:

```json
{
  "originalUrl": "https://learn.microsoft.com/aspnet/core",
  "customAlias": "aspnet-notes",
  "expiresAt": "2026-12-31T23:59:59Z"
}
```

`customAlias` and `expiresAt` are optional. Aliases must contain 4–32 letters, numbers, underscores, or hyphens.

## Run with Visual Studio or the .NET CLI

Requirements: .NET 8 SDK and SQL Server or LocalDB.

1. Configure `ConnectionStrings:DefaultConnection` in `URLShortener.API/appsettings.json`.
2. Install the EF CLI if it is not already available:

   ```powershell
   dotnet tool install --global dotnet-ef
   ```

3. Apply migrations:

   ```powershell
   dotnet ef database update --project URLShortener.Infrastructure --startup-project URLShortener.API
   ```

4. Start the API and Web UI in separate terminals:

   ```powershell
   dotnet run --project URLShortener.API --launch-profile http
   dotnet run --project URLShortener.Web --launch-profile http
   ```

5. Open `http://localhost:5260` for the dashboard or `http://localhost:5137/swagger` for Swagger.

The existing initial migration is preserved. `20260819090000_AddAnalytics` expands the code column, adds link state fields, and creates `ClickEvents` without deleting existing link data.

### Address already in use

A Kestrel `Socket.Bind` or `address already in use` exception means another process is already listening on the configured port. This usually happens when the API or UI is started twice. Reuse the running instance or press `Ctrl+C` in its terminal before starting it again.

To inspect the local development ports on Windows:

```powershell
netstat -ano | findstr ":5137 :5260"
```

If you intentionally change the API port, update both `URLShortener.API/Properties/launchSettings.json` and `URLShortener.Web/appsettings.json` so the UI calls the same address.

## Run with Docker

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Change the example SQL Server password before starting. The UI is available at `http://localhost:8081`, the API at `http://localhost:8080`, and migrations are applied automatically in the API container.

## Reusing the service

Other websites, mobile applications, and backend services should call `URLShortener.API` over HTTP rather than referencing Infrastructure or connecting to its database. See `INTEGRATION_GUIDE.md` for .NET and JavaScript examples, deployment guidance, and the recommended client-SDK boundary.

## Tests

```powershell
dotnet test URLShortener.sln
```

The tests cover custom aliases, duplicate aliases, redirect tracking, browser/device/referrer classification, expiration behavior, privacy-aware visitor IDs, URL-scheme validation, migration discovery, and UI API-client behavior.

## Important next learning step

The management endpoints are intentionally unauthenticated so the project stays focused on backend fundamentals. Before exposing it publicly, protect link listing, status changes, and analytics with authentication and authorization. Good extensions are JWT authentication, per-user ownership, Redis caching, queued analytics ingestion, and integration tests against a disposable SQL Server container.
