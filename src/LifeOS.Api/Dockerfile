# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/LifeOS.sln .
COPY src/LifeOS.Domain/LifeOS.Domain.csproj LifeOS.Domain/
COPY src/LifeOS.Application/LifeOS.Application.csproj LifeOS.Application/
COPY src/LifeOS.Infrastructure/LifeOS.Infrastructure.csproj LifeOS.Infrastructure/
COPY src/LifeOS.Api/LifeOS.Api.csproj LifeOS.Api/
RUN dotnet restore LifeOS.Api/LifeOS.Api.csproj

COPY src/ .
RUN dotnet publish LifeOS.Api/LifeOS.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Ensure data directories exist
RUN mkdir -p /app/data/generated /app/data/uploads

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "LifeOS.Api.dll"]
