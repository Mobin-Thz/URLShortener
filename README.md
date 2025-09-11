# URL Shortener API

یک سرویس کوتاه‌کننده لینک با ASP.NET Core Web API و Entity Framework Core است که لینک‌های طولانی را به لینک‌های کوتاه و منحصر به فرد تبدیل می‌کند و امکان هدایت (Redirect) به لینک اصلی را فراهم می‌کند.

---

## ویژگی‌ها

- کوتاه کردن لینک با کد کوتاه غیر ترتیبی.
- اعتبارسنجی ورودی (URL باید معتبر باشد).
- ریدایرکت به لینک اصلی با HTTP 302.
- مدیریت خطاهای 404 و 400.
- طراحی با Clean Architecture (Api, Application, Domain, Infrastructure layers).

---

## تکنولوژی‌ها

- .NET 8 / ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Swagger / OpenAPI

---

## راه‌اندازی و اجرا

1. کلون کردن ریپازیتوری:
```bash
git clone https://github.com/username/URLShortener.git
cd URLShortener
```

2. تنظیم Connection String در `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=URLShortenerDb;Trusted_Connection=True;"
}
```

3. نصب پکیج‌های EF Core در پروژه Infrastructure:

```powershell
Install-Package Microsoft.EntityFrameworkCore
Install-Package Microsoft.EntityFrameworkCore.SqlServer
Install-Package Microsoft.EntityFrameworkCore.Design
Install-Package Microsoft.EntityFrameworkCore.Tools

```

4. ایجاد Migration و بروزرسانی دیتابیس:

```powershell
Add-Migration InitialCreate -StartupProject URLShortener.API
Update-Database -StartupProject URLShortener.API
```

5. اجرای پروژه:

```bash
dotnet run --project URLShortener.API
```

* Swagger UI: `https://localhost:7074/swagger/index.html`

---

## Endpointها

### POST /api/URLShortener/Shorten

**Body:**

```json
{
  "originalUrl": "https://github.com/Mobin-Thz"
}
```

**Response 200 OK:**

```json
{
  "originalUrl": "https://github.com/Mobin-Thz",
  "shortUrl": "https://localhost:7074/ErXuJ7",
  "code": "ErXuJ7",
  "createdAt": "2025-09-11T12:28:35.84953Z"
}
```

**خطاها:**

* 400 Bad Request → URL نامعتبر

### GET /api/URLShortener/RedirectToOriginal/{shortCode}

* اگر لینک پیدا شد → HTTP 302 Redirect
* اگر لینک پیدا نشد → HTTP 404 Not Found:

```json
{
  "message": "URL not found."
}
```

---

## Postman Collection

* فایل `URLShortener.postman_collection.json` در فولدر `PostmanCollection` شامل درخواست‌های POST و GET برای تست سریع endpointها.

---

## الگوریتم تولید کد کوتاه

* تولید رشته تصادفی 6 کاراکتری از حروف و اعداد `[a-zA-Z0-9]`.
* بررسی یکتا بودن کد در دیتابیس قبل از بازگرداندن.
* حلقه while برای مدیریت تصادم و تضمین تولید کد منحصر به فرد.

```csharp
while (true)
{
    var code = GenerateRandomCode();
    if (!await _dbContext.ShortenedUrls.AnyAsync(s => s.Code == code))
        return code;
}
```
