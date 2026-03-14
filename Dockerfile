# ── Etapa 1: Build ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias (aprovecha cache de Docker)
COPY FloreriaBautista.csproj .
RUN dotnet restore

# Copiar todo el código y compilar en Release
COPY . .
RUN dotnet publish FloreriaBautista.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Etapa 2: Runtime ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar postgresql-client para pg_dump y pg_restore
RUN apt-get update && apt-get install -y --no-install-recommends \
    postgresql-client \
    && rm -rf /var/lib/apt/lists/*

# Copiar el build
COPY --from=build /app/publish .

# Copiar credenciales de Google Drive si existen
# (también se pueden montar como volumen en producción)
COPY google_credentials.json* ./

# Carpeta de backups locales (se puede montar como volumen)
RUN mkdir -p /app/backups

# Puerto de la app
EXPOSE 5000
EXPOSE 5001

# Variables de entorno base (las sensibles van en .env o docker-compose)
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FloreriaBautista.dll"]