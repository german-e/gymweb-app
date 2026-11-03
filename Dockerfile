# syntax=docker/dockerfile:1
# =====================================================================
#  GymCore — imagen de producción (ASP.NET Core 10 · Razor Pages)
#  Build:  docker build -t gymcore:1.0.0 .
#  Tests:  docker build --target test .
# =====================================================================

# ---------- Etapa 1: restore + build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Solo los archivos de proyecto primero: el restore queda cacheado
# mientras no cambien las dependencias.
COPY global.json GymCore.sln ./
COPY src/GymCore.Dominio/GymCore.Dominio.csproj               src/GymCore.Dominio/
COPY src/GymCore.Aplicacion/GymCore.Aplicacion.csproj         src/GymCore.Aplicacion/
COPY src/GymCore.Infraestructura/GymCore.Infraestructura.csproj src/GymCore.Infraestructura/
COPY src/GymCore.Web/GymCore.Web.csproj                       src/GymCore.Web/
COPY tests/GymCore.Tests/GymCore.Tests.csproj                 tests/GymCore.Tests/
RUN dotnet restore GymCore.sln

# Resto del código
COPY . .
RUN dotnet build GymCore.sln -c Release --no-restore

# ---------- Etapa opcional: pruebas (docker build --target test .) ----------
FROM build AS test
RUN dotnet test GymCore.sln -c Release --no-build --logger "console;verbosity=normal"

# ---------- Etapa 2: publish ----------
FROM build AS publish
RUN dotnet publish src/GymCore.Web/GymCore.Web.csproj \
      -c Release -o /app/publish --no-build /p:UseAppHost=false

# ---------- Etapa 3: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl solo para el HEALTHCHECK (la imagen base no trae curl ni wget)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    TZ=America/Argentina/Buenos_Aires \
    ConnectionStrings__Default="Data Source=/app/data/gym.db" \
    DataProtection__Ruta=/app/keys \
    Almacenamiento__Uploads=/app/uploads

COPY --from=publish /app/publish .

# Carpetas persistentes (se montan como volúmenes), con permisos para el usuario sin privilegios 'app':
#   /app/data     -> base SQLite
#   /app/keys     -> claves de Data Protection (cookies de sesión)
#   /app/uploads  -> fotos e imágenes subidas (fuera de wwwroot: las fotos exigen sesión)
RUN mkdir -p /app/data /app/keys /app/uploads \
 && chown -R app:app /app/data /app/keys /app/uploads

USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fs http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "GymCore.Web.dll"]
