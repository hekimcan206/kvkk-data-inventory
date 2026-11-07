# syntax=docker/dockerfile:1

# ---- build -------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer: it only re-runs when package versions or project files change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/KvkkInventory.Core/KvkkInventory.Core.csproj src/KvkkInventory.Core/
COPY src/KvkkInventory.Postgres/KvkkInventory.Postgres.csproj src/KvkkInventory.Postgres/
COPY src/KvkkInventory.Oracle/KvkkInventory.Oracle.csproj src/KvkkInventory.Oracle/
COPY src/KvkkInventory.Synthetic/KvkkInventory.Synthetic.csproj src/KvkkInventory.Synthetic/
COPY src/KvkkInventory.Api/KvkkInventory.Api.csproj src/KvkkInventory.Api/
COPY src/KvkkInventory.Cli/KvkkInventory.Cli.csproj src/KvkkInventory.Cli/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/KvkkInventory.Api/KvkkInventory.Api.csproj && \
    dotnet restore src/KvkkInventory.Cli/KvkkInventory.Cli.csproj

COPY .editorconfig ./
COPY samples/ samples/
COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/KvkkInventory.Api/KvkkInventory.Api.csproj -c Release --no-restore -o /app/api -p:UseAppHost=false && \
    dotnet publish src/KvkkInventory.Cli/KvkkInventory.Cli.csproj -c Release --no-restore -o /app/cli -p:UseAppHost=false
# Output directory for the compose demo, owned by the non-root app user so a named volume mounted there is writable.
RUN mkdir -p /out

# ---- runtime -----------------------------------------------------------------------------------------
# Chiseled Ubuntu: no shell, no package manager, non-root "app" user (UID 1654).
# One image, two entry points: the API (default) and the `kvkk` CLI (`dotnet /app/cli/kvkk.dll …`).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="kvkk-data-inventory" \
      org.opencontainers.image.source="https://github.com/hekimcan206/kvkk-data-inventory" \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=build --link /app/api /app/api
COPY --from=build --link /app/cli /app/cli
COPY --from=build --link /src/samples /app/samples
COPY --from=build --chown=1654:1654 /out /out
USER $APP_UID
EXPOSE 8080
WORKDIR /app/api
ENTRYPOINT ["dotnet", "/app/api/KvkkInventory.Api.dll"]
