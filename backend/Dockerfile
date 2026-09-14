# B2BShop API — multi-stage build
# Build context: ./backend (from docker-compose)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY src/B2BShop.Domain/B2BShop.Domain.csproj           src/B2BShop.Domain/
COPY src/B2BShop.Application/B2BShop.Application.csproj src/B2BShop.Application/
COPY src/B2BShop.Infrastructure/B2BShop.Infrastructure.csproj src/B2BShop.Infrastructure/
COPY src/B2BShop.Api/B2BShop.Api.csproj                 src/B2BShop.Api/

RUN dotnet restore src/B2BShop.Api/B2BShop.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/B2BShop.Api/B2BShop.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy schema extras SQL file so DbInitializer can apply it at startup
COPY src/B2BShop.Infrastructure/Migrations/ Migrations/

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80

ENTRYPOINT ["dotnet", "B2BShop.Api.dll"]
