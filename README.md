# [View Website](https://lopezautosales.com)

A point-of-sale and inventory web app for a car dealership: a public inventory site
plus an admin app for sales paperwork, payment accounts, and inventory.

# Tech
* Front end:
  * Blazor WebAssembly (admin app, under `/app`)
  * ASP.NET Core Razor Pages (public inventory site)
  * Bootstrap 5
* Back end:
  * .NET 10 (ASP.NET Core Web API + Razor Pages, hosts the WASM client)
  * ASP.NET Core Identity with cookie authentication
  * Entity Framework Core + PostgreSQL (Npgsql)
  * Cloudflare R2 (S3-compatible) for vehicle images
  * Serilog (console)
* Hosting: Docker container on Railway

# Projects
* `LopezAutoSales/Server` — API, public Razor Pages, hosts the Blazor client
* `LopezAutoSales/Client` — Blazor WebAssembly admin app
* `LopezAutoSales/Shared` — models/DTOs shared by both

# Features
* Point of sale: generate printable sales papers, receipts, and warranties; manage
  payment accounts
* Inventory management with image uploads to object storage
* Single-admin authentication (cookie-based)

# Deploying
See `docs/railway-deploy.md` (hosting + database) and `docs/r2-images.md` (image storage).
