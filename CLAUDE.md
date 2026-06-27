# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A point-of-sale and inventory web app for a single car dealership (Lopez Auto Sales).
.NET 10, Blazor WebAssembly **hosted** model. Deployed as a Docker container on Railway
with managed PostgreSQL; vehicle images live in Cloudflare R2.

## Architecture (the big picture)

Three projects under `LopezAutoSales/`, reference graph `Shared ← Client ← Server`:

- **`Server`** (`Microsoft.NET.Sdk.Web`) — does three jobs in one process:
  1. The **public site** as ASP.NET Core **Razor Pages** (`Pages/Index|View|About`) — server-rendered inventory for customers.
  2. The **API** (`Controllers/`) consumed by the admin SPA.
  3. **Hosts the Blazor WASM client** (`UseBlazorFrameworkFiles` + `MapFallbackToFile("index.html")`).
  Routing order in `Startup.Configure`: Razor Pages → controllers → WASM fallback. So `/` and `/view/{id}` are server-rendered; everything unmatched serves the SPA shell.
- **`Client`** — the Blazor WASM **admin app**, all routes under **`/app`** (e.g. `/app/inventory`, `/app/papers`). This is the only thing behind auth.
- **`Shared`** — entities/DTOs used by both sides as the wire contract (e.g. `Car`, `Sale`, `Account`, `Payment`, `Picture`). These double as EF entities; `Car` etc. carry `[Column]`/`[NotMapped]` annotations.

Uses the **classic `Startup.cs` + `Program.CreateHostBuilder` generic host**, not minimal hosting. Don't "modernize" it to minimal hosting without reason.

### Auth (cookie-based, single admin)
Plain ASP.NET Core Identity with the **application cookie** — no JWT/IdentityServer. One admin user is seeded on startup from the `Admin__Password` env var. Flow:
- Client posts to `AuthController` (`/api/auth/login|logout|me|change-password`).
- `Client/CookieAuthenticationStateProvider` determines auth by calling `/api/auth/me`; `Client/CookieHandler` (a `DelegatingHandler`) attaches the cookie to API calls; `Client/AuthHttp` wraps that `HttpClient`.
- API paths return **401/403** instead of redirecting (`ConfigureApplicationCookie` events). Admin endpoints are gated with `[Authorize(Roles = "Admin")]`; admin Blazor pages each carry `@attribute [Authorize(Roles = "Admin")]` (the `MainLayout` attribute is a no-op — per-page is what's enforced).
- The `"NoAuth"` named HttpClient deliberately has **no** cookie handler (used for the external NHTSA VIN lookup in `Services/VINDecoder`).

### Data (EF Core + Postgres)
`ApplicationDbContext : IdentityDbContext<ApplicationUser>`. `Startup.Configure` runs `Database.Migrate()` + seeds the admin on startup. Connection string comes from `DATABASE_URL` (Railway URL form, parsed by `Server/Data/NpgsqlUrl`) or `ConnectionStrings:DefaultConnection` locally.

### Images (Cloudflare R2)
`Server/Storage/IImageStorage` + `R2ImageStorage` (AWS S3 SDK, S3-compatible). **`Picture.URL` stores the object *key*** (e.g. `Images/<guid>.jpg`); it is resolved to an absolute public URL via `IImageStorage.PublicUrl` only when read for display (`InventoryController.ResolveUrls`, the public PageModels). Never persist the resolved URL — `ResolveUrls` is only safe on `AsNoTracking` results or after `SaveChanges`.

## Commands

```bash
# Build / test (run from repo root). SDK is pinned by global.json (.NET 10).
dotnet build LopezAutoSales.sln -c Debug
dotnet test  LopezAutoSales.sln
dotnet test  LopezAutoSales.sln --filter "FullyQualifiedName~NpgsqlUrlTests"   # single test class

# EF migrations (run from LopezAutoSales/Server). dotnet-ef is pinned in .config/dotnet-tools.json.
# A dummy DATABASE_URL is enough — `migrations add` doesn't connect.
dotnet tool restore
DATABASE_URL="postgresql://u:p@localhost:5432/lopez" dotnet dotnet-ef migrations add <Name>
DATABASE_URL="postgresql://u:p@localhost:5432/lopez" dotnet dotnet-ef migrations has-pending-model-changes
```

### Run / smoke-test locally (Docker + Postgres)
There is no local SQL Server; the app needs Postgres. Build the image and run it against a throwaway Postgres. Use `ASPNETCORE_ENVIRONMENT=Development` so the auth cookie isn't `Secure` (otherwise login won't work over plain HTTP):

```bash
docker build -t lopezautosales:test .
docker network create lopeznet
docker run -d --name lopez-pg --network lopeznet -e POSTGRES_PASSWORD=secret postgres:16
docker run -d --name lopez-app --network lopeznet -p 8099:8080 -e ASPNETCORE_ENVIRONMENT=Development \
  -e "DATABASE_URL=postgresql://postgres:secret@lopez-pg:5432/postgres" -e "Admin__Password=Test1234!" \
  -e "ObjectStorage__ServiceUrl=https://example.r2.cloudflarestorage.com" \
  -e "ObjectStorage__PublicBaseUrl=https://img.example.com" -e "ObjectStorage__Bucket=test" \
  -e "Stripe__SecretKey=sk_test_xxx" -e "Stripe__PublishableKey=pk_test_xxx" -e "Stripe__WebhookSecret=whsec_xxx" \
  lopezautosales:test
# app at http://localhost:8099 ; admin login lopezauto@outlook.com / the Admin__Password value
# Stripe is OPTIONAL — omit the Stripe__* vars and the app still runs; the public /pay page
# just shows "online payments unavailable". Use `stripe listen` to get a real whsec_… to test webhooks.
```

Deployment runbooks: `docs/railway-deploy.md` (hosting + DB + data migration) and `docs/r2-images.md` (image storage).

## Conventions & gotchas (non-obvious, easy to break)

- **DateTimes are local-time `timestamp` (no time zone).** `Npgsql.EnableLegacyTimestampBehavior` is set in **`CreateHostBuilder`** (not `Main`) on purpose — EF's design-time tooling calls `CreateHostBuilder` but not `Main`, and design-time vs runtime must build the same model. If you change DateTime mapping, re-generate migrations.
- **Don't remove the pinned `ConcurrencyStamp` on the seeded `IdentityRole`** (`ApplicationDbContext`). `IdentityRole` generates a random stamp each model build; an unpinned value makes the model non-deterministic and breaks `migrations add` / startup migration.
- **Cookie `SecurePolicy` is environment-aware** — `Always` in production, `SameAsRequest` in Development. Local HTTP login only works in Development.
- **Razor Pages: `page` is a reserved route token** — don't name a query-string/handler parameter `page` (it won't bind).
- **Server-rendered currency is culture-sensitive.** The app pins `en-US` at startup so `ToString("C")` renders `$` on the Linux container (defaults to InvariantCulture → `¤`).
- **`SixLabors.ImageSharp` is intentionally kept on the 2.x line** (Apache-2.0). 3.x/4.x switched to a paid commercial license — do not upgrade it.
- **Secrets** (`Admin__Password`, `ObjectStorage__*`, `Stripe__*`, `DATABASE_URL`) come from env vars / user-secrets; `appsettings.json` ships only empty placeholders. `Stripe__*` (SecretKey/PublishableKey/WebhookSecret) are **TEST**-mode keys for the public `/pay` online-payment flow; they're optional (bound but not `ValidateOnStart`), so the app runs without them and `/pay` degrades gracefully.
- After image uploads/deletes, object-storage and DB writes are **not transactional** (two systems). Delete blobs after a successful `SaveChanges` and tolerate orphans.
