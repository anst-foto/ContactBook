# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ContactBook.WebApi/ContactBook.WebApi.csproj", "ContactBook.WebApi/"]
RUN dotnet restore "ContactBook.WebApi/ContactBook.WebApi.csproj"

COPY . .
WORKDIR "/src/ContactBook.WebApi"
RUN dotnet publish "ContactBook.WebApi.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --property:UseAppHost=false

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
