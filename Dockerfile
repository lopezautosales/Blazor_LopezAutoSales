# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless a .csproj changes)
COPY LopezAutoSales/Server/LopezAutoSales.Server.csproj LopezAutoSales/Server/
COPY LopezAutoSales/Client/LopezAutoSales.Client.csproj LopezAutoSales/Client/
COPY LopezAutoSales/Shared/LopezAutoSales.Shared.csproj LopezAutoSales/Shared/
RUN dotnet restore LopezAutoSales/Server/LopezAutoSales.Server.csproj

# Publish the server (bundles the Blazor WASM client into wwwroot)
COPY . .
RUN dotnet publish LopezAutoSales/Server/LopezAutoSales.Server.csproj -c Release -o /app /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Npgsql probes for Kerberos/GSSAPI at startup; curl is used by the healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
# Run as the non-root user the aspnet image ships with.
RUN chown -R app:app /app
USER app
# Railway overrides PORT at runtime; 8080 is the local default.
ENV PORT=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT}/health" || exit 1
ENTRYPOINT ["dotnet", "LopezAutoSales.Server.dll"]
