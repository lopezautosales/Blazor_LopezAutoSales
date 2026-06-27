---
name: smoke-test
description: Build and run LopezAutoSales in Docker against a throwaway Postgres, then verify the app (login, sell/edit a vehicle) — optionally driving it in Chrome. Use when asked to run, smoke-test, or verify a change works in the real app (there is no local SQL Server, so the app must run against Postgres in a container).
---

# Smoke-test the app (Docker + Postgres)

There is no local database; the app needs Postgres. Build the image and run it against a
throwaway Postgres container. Use `ASPNETCORE_ENVIRONMENT=Development` so the auth cookie is
not `Secure` — otherwise login won't work over plain HTTP and every verification fails.

## Stand it up

```bash
cd "<repo root>"
docker build -t lopezautosales:test .
docker rm -f lopez-pg lopez-app >/dev/null 2>&1 || true
docker network create lopeznet >/dev/null 2>&1 || true
docker run -d --name lopez-pg --network lopeznet -e POSTGRES_PASSWORD=secret postgres:16
for i in $(seq 1 20); do docker exec lopez-pg pg_isready -U postgres >/dev/null 2>&1 && break; sleep 2; done
docker run -d --name lopez-app --network lopeznet -p 8099:8080 -e ASPNETCORE_ENVIRONMENT=Development \
  -e "DATABASE_URL=postgresql://postgres:secret@lopez-pg:5432/postgres" -e "Admin__Password=Test1234!" \
  -e "ObjectStorage__ServiceUrl=https://example.r2.cloudflarestorage.com" \
  -e "ObjectStorage__PublicBaseUrl=https://img.example.com" -e "ObjectStorage__Bucket=test" \
  lopezautosales:test
for i in $(seq 1 30); do [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:8099/ 2>/dev/null)" = "200" ] && break; sleep 2; done
```

On first boot the app applies EF migrations and seeds the admin (`lopezauto@outlook.com` /
the `Admin__Password` value). The dummy `ObjectStorage__*` values are fine for everything
except actual image upload/serving (R2 isn't reachable), so avoid image steps unless you
provide real R2 credentials.

## Verify the API quickly (no browser)

```bash
curl -s -c /tmp/c.txt -o /dev/null -X POST http://localhost:8099/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"lopezauto@outlook.com","password":"Test1234!","rememberMe":false}'
curl -s -b /tmp/c.txt http://localhost:8099/api/auth/me        # -> {"isAuthenticated":true,...,"roles":["Admin"]}
# seed a car for UI flows:
curl -s -b /tmp/c.txt -X POST http://localhost:8099/api/inventory -H 'Content-Type: application/json' \
  -d '{"year":2020,"make":"Toyota","model":"Camry","color":"Blue","vin":"4T1BF1FK5LU000001","mileage":35000,"listPrice":18000}'
```

## Verify in the browser (Chrome automation)

Use the `claude-in-chrome` tools (load them via ToolSearch first). Key flows worth checking:
- Public inventory at `/` renders cards (server-rendered Razor; currency should be `$`).
- Log in at `/app/login`, then the admin grid `/app/inventory` (deserializes via System.Text.Json).
- Sell a car: `/app/inventory/sell/{id}` → fill buyer + address (required) → **Complete Sale** → lands on `/app/papers/view/{id}`.
- Edit: `/app/papers/edit/{id}` loads the sale into the same `Shared/SaleForm` component → submit (PUT).
- Check `read_console_messages` with `onlyErrors: true` for any client exceptions.

Note: `Services/CarManager` caches cars in session storage with no invalidation, so a reused
browser tab can show a stale car in the sell form — start a fresh tab if data looks off.

## Tear down

```bash
docker rm -f lopez-app lopez-pg >/dev/null 2>&1
docker network rm lopeznet >/dev/null 2>&1
docker rmi lopezautosales:test >/dev/null 2>&1
```
