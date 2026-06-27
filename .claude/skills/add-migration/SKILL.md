---
name: add-migration
description: Add or manage an EF Core (Npgsql/Postgres) migration for LopezAutoSales after changing ApplicationDbContext or any Shared entity. Use when the model changes, when "migrations has pending changes" appears, or when startup fails with PendingModelChangesWarning.
---

# Add an EF Core migration

The provider is **Npgsql/Postgres**. `dotnet-ef` is pinned in
`LopezAutoSales/Server/.config/dotnet-tools.json`. `migrations add` does **not** connect to a
database, so a dummy `DATABASE_URL` is enough — the design-time host just builds the model.

```bash
cd "<repo root>/LopezAutoSales/Server"
dotnet tool restore                       # once per clone
export DATABASE_URL="postgresql://u:p@localhost:5432/lopez"
dotnet dotnet-ef migrations add <Name>
dotnet dotnet-ef migrations has-pending-model-changes   # must report "No changes"
```

Migrations are applied automatically at runtime by `Database.Migrate()` in `Startup.Configure`,
so you don't run `database update` for normal deploys — just commit the migration.

## Gotchas that cause migration churn / startup failures

- **`PendingModelChangesWarning` at startup or design-time `has-pending` keeps reporting changes**
  even when you didn't touch the schema: the model is **non-deterministic**. The known causes here:
  - The seeded `IdentityRole` must keep its **pinned `ConcurrencyStamp`** (`ApplicationDbContext`);
    `new IdentityRole{...}` otherwise generates a random stamp each model build.
  - Don't move `Npgsql.EnableLegacyTimestampBehavior` out of `Program.CreateHostBuilder`. Design-time
    tooling calls `CreateHostBuilder` but not `Main`; if the switch isn't set there, the design-time
    model maps `DateTime` to `timestamptz` while runtime maps to `timestamp`, so they never match.
- **Seed data must be static** — never seed an entity built from a mutated shared static or any
  runtime value (`DateTime.Now`, `Guid.NewGuid()`).
- `UseXminAsConcurrencyToken` was **removed in Npgsql 10**; don't reach for it.

## Inspecting a migration before committing

```bash
sed -n '/protected override void Up/,/protected override void Down/p' Migrations/*_<Name>.cs
```

To throw one away: `dotnet dotnet-ef migrations remove` (most recent), or delete the
`Migrations/` files and regenerate if the DB has never had it applied.
