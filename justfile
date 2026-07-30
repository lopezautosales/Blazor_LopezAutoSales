# Lopez Auto Sales -- common tasks. Run `just` (or `just --list`) to see them all.
#
# Recipes run through Windows PowerShell, matching the .ps1 scripts in scripts/.

set shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

# List available recipes
default:
    @just --list

# Build the solution (Debug)
build:
    dotnet build LopezAutoSales.sln -c Debug

# Run the test suite
test:
    dotnet test LopezAutoSales.sln

# Run a single test class, e.g. `just test-one NpgsqlUrlTests`
test-one CLASS:
    dotnet test LopezAutoSales.sln --filter "FullyQualifiedName~{{CLASS}}"


# A dummy DATABASE_URL is enough here -- `migrations add` never connects.
# Add an EF migration, e.g. `just migration AddPaymentNotes`
migration NAME:
    cd LopezAutoSales/Server; $env:DATABASE_URL = "postgresql://u:p@localhost:5432/lopez"; dotnet dotnet-ef migrations add {{NAME}}

# Fail if the EF model has drifted from the migrations
migration-check:
    cd LopezAutoSales/Server; $env:DATABASE_URL = "postgresql://u:p@localhost:5432/lopez"; dotnet dotnet-ef migrations has-pending-model-changes

# Tail Railway logs (default: the app; pass Postgres for the database)
logs SERVICE="Blazor_LopezAutoSales":
    railway logs --service {{SERVICE}}

# Railway CPU/memory/HTTP metrics (default: the app)
metrics SERVICE="Blazor_LopezAutoSales":
    railway metrics --service {{SERVICE}}

# Is the public site actually serving?
health:
    @curl.exe -s -o NUL -w "lopezautosales.com -> HTTP %{http_code} in %{time_total}s`n" https://lopezautosales.com/
