# Etapa de build: compila y publica la API.
# El contexto de build es la RAÍZ del repo porque la API referencia los
# proyectos Core.Domain, Core.Application y Core.Infrastructure.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar solo los .csproj primero: si no cambian, Docker reusa la capa de restore.
COPY ["Orkin.Cotizador.Core.Domain/Orkin.Cotizador.Core.Domain.csproj", "Orkin.Cotizador.Core.Domain/"]
COPY ["Orkin.Cotizador.Core.Application/Orkin.Cotizador.Core.Application.csproj", "Orkin.Cotizador.Core.Application/"]
COPY ["Orkin.Cotizador.Core.Infrastructure/Orkin.Cotizador.Core.Infrastructure.csproj", "Orkin.Cotizador.Core.Infrastructure/"]
COPY ["Orkin.Cotizador.Api/Orkin.Cotizador.Api.csproj", "Orkin.Cotizador.Api/"]

RUN dotnet restore "Orkin.Cotizador.Api/Orkin.Cotizador.Api.csproj"

# Copiar el resto del código y publicar.
COPY . .
RUN dotnet publish "Orkin.Cotizador.Api/Orkin.Cotizador.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Etapa de runtime: imagen final, sin SDK ─────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# La semilla de catálogos (scripts/seed/*.json) va en /app/scripts/seed:
# es donde el DbInitializer la busca al arrancar con RUN_SEED=true.
COPY --from=build /src/scripts/seed /app/scripts/seed

# .NET en contenedor escucha en 8080 por defecto (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080

ENTRYPOINT ["dotnet", "Orkin.Cotizador.Api.dll"]
