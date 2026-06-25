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
# Npgsql probes for Kerberos/GSSAPI at startup; provide it on the slim image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
# Railway overrides PORT at runtime; 8080 is the local default.
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LopezAutoSales.Server.dll"]
