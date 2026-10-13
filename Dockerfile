# syntax=docker/dockerfile:1

# --- Frontend build ---
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY Frontend/package*.json ./
RUN npm install
COPY Frontend/ ./
RUN npm run build
# Артефакты в /ContactBook.WebApi/wwwroot/

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ContactBook.WebApi/ContactBook.WebApi.csproj", "ContactBook.WebApi/"]
RUN dotnet restore "ContactBook.WebApi/ContactBook.WebApi.csproj"

COPY . .
COPY --from=frontend-build /ContactBook.WebApi/wwwroot ./ContactBook.WebApi/wwwroot

WORKDIR "/src/ContactBook.WebApi"
RUN dotnet publish "ContactBook.WebApi.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --property:UseAppHost=false

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "ContactBook.WebApi.dll"]