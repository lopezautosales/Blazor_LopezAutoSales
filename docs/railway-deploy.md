# Deploying to Railway (Docker + managed Postgres)

The app is a single container (the `Server` project, which bundles the Blazor WASM
client). It listens on `$PORT`, logs to stdout, applies EF migrations on startup, and
seeds the admin user. Images live in Cloudflare R2 (see `r2-images.md`).

## 1. Create the services

1. New Railway project → **Deploy from GitHub repo** (this repo). Railway detects the
   root `Dockerfile` and builds it.
2. Add a **PostgreSQL** database to the project (New → Database → PostgreSQL).

## 2. Environment variables (on the app service)

| Variable | Value |
|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` (reference the Postgres service) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Admin__Password` | the admin login password (min 6 chars, 1 upper, 1 digit, 1 symbol) |
| `ObjectStorage__ServiceUrl` | `https://<accountid>.r2.cloudflarestorage.com` |
| `ObjectStorage__Bucket` | `lopezautosales` |
| `ObjectStorage__PublicBaseUrl` | your public R2 URL |
| `ObjectStorage__AccessKey` | R2 access key id |
| `ObjectStorage__SecretKey` | R2 secret access key |

Railway injects `PORT` automatically — the app already reads it. On first deploy the app
creates the schema (`__EFMigrationsHistory` + tables) and seeds the admin user and role.

## 3. Migrating existing data from SQL Server

> Back up the SQL Server database first.

The first deploy creates an **empty** schema on Postgres. Copy only the domain data from
SQL Server into it with [pgloader](https://pgloader.io/) (`data only`, skipping the EF
history and the Identity tables, since the admin user is seeded by the app):

`migrate.load`:

```
LOAD DATABASE
  FROM    mssql://user:pass@OLD_SQLSERVER_HOST/LopezAutoSales
  INTO    postgresql://postgres:pass@RAILWAY_PG_HOST:PORT/railway

WITH      data only, include no drop, reset sequences

EXCLUDING TABLE NAMES MATCHING '__EFMigrationsHistory', ~/AspNet/, 'sysdiagrams';
```

Run it: `pgloader migrate.load`

`reset sequences` fixes the Postgres identity counters so new inserts don't collide with
imported ids. If you'd rather keep the seeded users only and import everything else, the
`EXCLUDING` line above already skips `AspNet*` — if you instead want to import users too,
delete the seeded admin row first and drop `~/AspNet/` from the exclude list.

## 4. Cut over

1. Verify the Railway URL loads the public inventory and you can log in at `/app/login`.
2. Point your domain at the Railway service (custom domain in the Railway dashboard).
3. Decommission SmarterASP.net.
