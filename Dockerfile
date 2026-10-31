# syntax=docker/dockerfile:1

# ---------- Frontend ----------
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
# vite.config.ts schreibt nach ../src/SchnapperRadar.Api/wwwroot
RUN npm run build

# ---------- Backend ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY src/ src/
RUN dotnet publish src/SchnapperRadar.Api/SchnapperRadar.Api.csproj -c Release -o /app /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=web /src/SchnapperRadar.Api/wwwroot ./wwwroot
RUN mkdir -p /data/keys && chown -R $APP_UID /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeysPath=/data/keys \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchnapperRadar.Api.dll"]
